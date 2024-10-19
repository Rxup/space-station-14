using System.Linq;
using Content.Server.Backmen.Disease.Components;
using Content.Server.Body.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Nutrition.Components;
using Content.Server.Popups;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Disease;
using Content.Shared.Backmen.Disease.Events;
using Content.Shared.Clothing.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rejuvenate;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Threading;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Disease;

/// <summary>
/// Handles disease propagation & curing
/// </summary>
public sealed class DiseaseSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ISerializationManager _serializationManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly IParallelManager _parallel = default!;

    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiseaseCarrierComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<DiseaseCarrierComponent, CureDiseaseAttemptEvent>(OnTryCureDisease);
        SubscribeLocalEvent<DiseaseCarrierComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<DiseasedComponent, ContactInteractionEvent>(OnContactInteraction);
        SubscribeLocalEvent<DiseasedComponent, EntitySpokeEvent>(OnEntitySpeak);
        SubscribeLocalEvent<DiseaseProtectionComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<DiseaseProtectionComponent, GotUnequippedEvent>(OnUnequipped);
        // Handling stuff from other systems
        SubscribeLocalEvent<DiseaseCarrierComponent, ApplyMetabolicMultiplierEvent>(OnApplyMetabolicMultiplier);

        _carrierQuery = GetEntityQuery<DiseaseCarrierComponent>();

        _cfg.OnValueChanged(CCVars.GameDiseaseEnabled, v=> _enabled = v, true);
    }

    private bool _enabled = true;

    private EntityQuery<DiseaseCarrierComponent> _carrierQuery;

    private readonly HashSet<EntityUid> _addQueue = new();
    private readonly HashSet<(Entity<DiseaseCarrierComponent> carrier, ProtoId<DiseasePrototype> disease)> _cureQueue = new();

    /// <summary>
    /// First, adds or removes diseased component from the queues and clears them.
    /// Then, iterates over every diseased component to check for their effects
    /// and cures
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled)
            return;

        foreach (var entity in _addQueue)
        {
            if (TerminatingOrDeleted(entity))
                continue;

            EnsureComp<DiseasedComponent>(entity);
        }
        _addQueue.Clear();

        foreach (var (carrier, disease) in _cureQueue)
        {
            var d = carrier.Comp.Diseases.FirstOrDefault(x => x.ID == disease);
            if (d != null)
            {
                carrier.Comp.Diseases.Remove(d);
                if (d.Infectious)
                {
                    carrier.Comp.PastDiseases.Add(disease);
                }
            }
            if (carrier.Comp.Diseases.Count == 0) //This is reliable unlike testing Count == 0 right after removal for reasons I don't quite get
                RemCompDeferred<DiseasedComponent>(carrier);
        }
        _cureQueue.Clear();

        var q = EntityQueryEnumerator<DiseasedComponent, DiseaseCarrierComponent, MobStateComponent, MetaDataComponent>();
        while (q.MoveNext(out var owner, out _, out var carrierComp, out var mobState, out var metaDataComponent))
        {
            if(Paused(owner, metaDataComponent))
                continue;

            if (carrierComp.Diseases.Count == 0)
            {
                continue;
            }

            //DebugTools.Assert(carrierComp.Diseases.Count > 0);
            /*
            if (_mobStateSystem.IsDead(owner, mobState))
            {
                if (_random.Prob(0.005f * frameTime)) //Mean time to remove is 200 seconds per disease
                    CureDisease((owner, carrierComp), _random.Pick(carrierComp.Diseases));

                continue;
            }
*/
            _parallel.ProcessNow(new DiseaseJob
            {
                System = this,
                Owner = (owner, carrierComp, mobState),
                FrameTime = frameTime
            },
                carrierComp.Diseases.Count);
        }
    }

    private record struct DiseaseJob : IParallelRobustJob
    {
        public DiseaseSystem System { get; init; }
        public Entity<DiseaseCarrierComponent, MobStateComponent> Owner { get; init; }
        public float FrameTime { get; init; }
        public void Execute(int index)
        {
            System.Process(Owner, FrameTime, index);
        }
    }

    private void Process(Entity<DiseaseCarrierComponent, MobStateComponent> owner, float frameTime, int i)
    {
        var disease = owner.Comp1.Diseases[i];
        disease.Accumulator += frameTime;
        disease.TotalAccumulator += frameTime;

        if (disease.Accumulator < disease.TickTime)
            return;

        // if the disease is on the silent disease list, don't do effects
        var doEffects = owner.Comp1.CarrierDiseases?.Contains(disease.ID) != true;
        disease.Accumulator -= disease.TickTime;

        var stage = 0; //defaults to stage 0 because you should always have one
        var lastThreshold = 0f;

        for (var j = 0; j < disease.Stages.Count; j++)
        {
            if (!(disease.TotalAccumulator >= disease.Stages[j]) || !(disease.Stages[j] > lastThreshold))
                continue;
            lastThreshold = disease.Stages[j];
            stage = j;
        }

        foreach (var cure in disease.Cures)
        {
            if (!cure.Stages.Contains(stage))
                continue;

            try
            {
                RaiseLocalEvent(owner, cure.GenerateEvent(owner, disease.ID));
            }
            catch (Exception err)
            {
                Log.Error(err.ToString());
            }
        }

        if (_mobStateSystem.IsIncapacitated(owner, owner))
            doEffects = false;

        if (!doEffects)
            return;

        foreach (var effect in disease.Effects)
        {
            if (!effect.Stages.Contains(stage) || !_random.Prob(effect.Probability))
                continue;

            try
            {
                RaiseLocalEvent(owner, effect.GenerateEvent(owner,disease.ID));
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
            }

        }
    }
        ///
        /// Event Handlers
        ///

        /// <summary>
        /// Fill in the natural immunities of this entity.
        /// </summary>
        private void OnInit(EntityUid uid, DiseaseCarrierComponent component, ComponentInit args)
        {
            if (component.NaturalImmunities == null || component.NaturalImmunities.Count == 0)
                return;

            foreach (var immunity in component.NaturalImmunities)
            {
                component.PastDiseases.Add(immunity);
            }
        }

        /// <summary>
        /// Used when something is trying to cure ANY disease on the target,
        /// not for special disease interactions. Randomly
        /// tries to cure every disease on the target.
        /// </summary>
        private void OnTryCureDisease(Entity<DiseaseCarrierComponent> ent, ref CureDiseaseAttemptEvent args)
        {
            foreach (var disease in ent.Comp.Diseases)
            {
                var cureProb = ((args.CureChance / ent.Comp.Diseases.Count) - disease.CureResist);
                if (cureProb < 0)
                    return;
                if (cureProb > 1)
                {
                    CureDisease(ent, disease);
                    return;
                }
                if (_random.Prob(cureProb))
                {
                    CureDisease(ent, disease);
                    return;
                }
            }
        }

        private void OnRejuvenate(EntityUid uid, DiseaseCarrierComponent component, RejuvenateEvent args)
        {
            CureAllDiseases(uid, component);
        }

        /// <summary>
        /// Called when a component with disease protection
        /// is equipped so it can be added to the person's
        /// total disease resistance
        /// </summary>
        private void OnEquipped(EntityUid uid, DiseaseProtectionComponent component, GotEquippedEvent args)
        {
            // This only works on clothing
            if (!TryComp<ClothingComponent>(uid, out var clothing))
                return;
            // Is the clothing in its actual slot?
            if (!clothing.Slots.HasFlag(args.SlotFlags))
                return;
            // Give the user the component's disease resist
            if(TryComp<DiseaseCarrierComponent>(args.Equipee, out var carrier))
                carrier.DiseaseResist += component.Protection;
            // Set the component to active to the unequip check isn't CBT
            component.IsActive = true;
        }

        /// <summary>
        /// Called when a component with disease protection
        /// is unequipped so it can be removed from the person's
        /// total disease resistance
        /// </summary>
        private void OnUnequipped(EntityUid uid, DiseaseProtectionComponent component, GotUnequippedEvent args)
        {
            // Only undo the resistance if it was affecting the user
            if (!component.IsActive)
                return;
            if(TryComp<DiseaseCarrierComponent>(args.Equipee, out var carrier))
                carrier.DiseaseResist -= component.Protection;
            component.IsActive = false;
        }

        /// <summary>
        /// Called when it's already decided a disease will be cured
        /// so it can be safely queued up to be removed from the target
        /// and added to past disease history (for immunity)
        /// </summary>
        public void CureDisease(Entity<DiseaseCarrierComponent> carrier, DiseasePrototype disease)
        {
            CureDisease(carrier, disease.ID);
        }
        public void CureDisease(Entity<DiseaseCarrierComponent> carrier, ProtoId<DiseasePrototype> disease)
        {
            _cureQueue.Add((carrier, disease));
            _popupSystem.PopupEntity(Loc.GetString("disease-cured"), carrier.Owner, carrier.Owner);
        }

        public void CureAllDiseases(EntityUid uid, DiseaseCarrierComponent? carrier = null)
        {
            if (!Resolve(uid, ref carrier))
                return;

            foreach (var disease in carrier.Diseases)
            {
                CureDisease((uid,carrier), disease);
            }
        }

        /// <summary>
        /// When a diseased person interacts with something, check infection.
        /// </summary>
        private void OnContactInteraction(EntityUid uid, DiseasedComponent component, ContactInteractionEvent args)
        {
            InteractWithDiseased(uid, args.Other);
        }

        private void OnEntitySpeak(EntityUid uid, DiseasedComponent component, EntitySpokeEvent args)
        {
            if (TryComp<DiseaseCarrierComponent>(uid, out var carrier))
            {
                SneezeCough(uid, _random.Pick(carrier.Diseases).ID, string.Empty);
            }
        }

        /// <summary>
        /// Called when a vaccine is used on someone
        /// to handle the vaccination doafter
        /// </summary>
        private void OnAfterInteract(EntityUid uid, DiseaseVaccineComponent vaxx, AfterInteractEvent args)
        {
            if (args.Target == null || !args.CanReach || args.Handled)
                return;

            args.Handled = true;

            if (vaxx.Used)
            {
                _popupSystem.PopupEntity(Loc.GetString("vaxx-already-used"), args.User, args.User);
                return;
            }

            var ev = new VaccineDoAfterEvent();
            var doAfterArgs = new DoAfterArgs(EntityManager, args.User, vaxx.InjectDelay, ev, uid, target: args.Target, used: uid)
            {
                BreakOnMove = true,
                NeedHand = true
            };

            _doAfterSystem.TryStartDoAfter(doAfterArgs);
        }

        /// <summary>
        /// Called when a vaccine is examined.
        /// Currently doesn't do much because
        /// vaccines don't have unique art with a seperate
        /// state visualizer.
        /// </summary>
        private void OnExamined(EntityUid uid, DiseaseVaccineComponent vaxx, ExaminedEvent args)
        {
            if (args.IsInDetailsRange)
            {
                if (vaxx.Used)
                    args.PushMarkup(Loc.GetString("vaxx-used"));
                else
                    args.PushMarkup(Loc.GetString("vaxx-unused"));
            }
        }


    private void OnApplyMetabolicMultiplier(EntityUid uid, DiseaseCarrierComponent component, ApplyMetabolicMultiplierEvent args)
    {
        if (args.Apply)
        {
            foreach (var disease in component.Diseases)
            {
                disease.TickTime *= args.Multiplier;
                return;
            }
        }
        foreach (var disease in component.Diseases)
        {
            disease.TickTime /= args.Multiplier;
            if (disease.Accumulator >= disease.TickTime)
                disease.Accumulator = disease.TickTime;
        }
    }

        ///
        /// Helper functions
        ///

        /// <summary>
        /// Tries to infect anyone that
        /// interacts with a diseased person or body
        /// </summary>
        private void InteractWithDiseased(EntityUid diseased, EntityUid target, DiseaseCarrierComponent? diseasedCarrier = null)
        {
            if (!Resolve(diseased, ref diseasedCarrier, false) ||
                diseasedCarrier.Diseases.Count == 0 ||
                !TryComp<DiseaseCarrierComponent>(target, out var carrier))
                return;

            var disease = _random.Pick(diseasedCarrier.Diseases);
            TryInfect((target,carrier), disease, 0.4f);
        }

        /// <summary>
        /// Adds a disease to a target
        /// if it's not already in their current
        /// or past diseases. If you want this
        /// to not be guaranteed you are looking
        /// for TryInfect.
        /// </summary>
        public void TryAddDisease(EntityUid host, DiseasePrototype addedDisease, DiseaseCarrierComponent? target = null)
        {
            TryAddDisease(host, addedDisease.ID, target);
        }

        public void TryAddDisease(EntityUid host, ProtoId<DiseasePrototype> addedDisease, DiseaseCarrierComponent? target = null)
        {
            if (!Resolve(host, ref target, false))
                return;

            foreach (var disease in target.AllDiseases)
            {
                if (disease == addedDisease) //ID because of the way protoypes work
                    return;
            }

            if (!_prototypeManager.TryIndex(addedDisease, out var added))
                return;
            var freshDisease = _serializationManager.CreateCopy(added, notNullableOverride: true);

            //if (freshDisease == null)
            //    return;

            target.Diseases.Add(freshDisease);
            _addQueue.Add(host);
        }

        /// <summary>
        /// Pits the infection chance against the
        /// person's disease resistance and
        /// rolls the dice to see if they get
        /// the disease.
        /// </summary>
        /// <param name="carrier">The target of the disease</param>
        /// <param name="disease">The disease to apply</param>
        /// <param name="chance">% chance of the disease being applied, before considering resistance</param>
        /// <param name="forced">Bypass the disease's infectious trait.</param>
        public void TryInfect(Entity<DiseaseCarrierComponent> carrier, DiseasePrototype? disease, float chance = 0.7f, bool forced = false)
        {
            if(disease == null || !forced && !disease.Infectious)
                return;
            var infectionChance = chance - carrier.Comp.DiseaseResist;
            if (infectionChance <= 0)
                return;
            if (_random.Prob(infectionChance))
                TryAddDisease(carrier.Owner, disease, carrier);
        }

        public void TryInfect(Entity<DiseaseCarrierComponent> carrier, ProtoId<DiseasePrototype>? disease, float chance = 0.7f, bool forced = false)
        {
            if (disease == null || !_prototypeManager.TryIndex(disease, out var d))
                return;

            TryInfect(carrier, d, chance, forced);
        }

        /// <summary>
        /// Raises an event for systems to cancel the snough if needed
        /// Plays a sneeze/cough sound and popup if applicable
        /// and then tries to infect anyone in range
        /// if the snougher is not wearing a mask.
        /// </summary>
        public bool SneezeCough(EntityUid uid, ProtoId<DiseasePrototype> diseaseId, string emoteId, bool airTransmit = true, TransformComponent? xform = null)
        {
            if (!Resolve(uid, ref xform))
                return false;

            if (_mobStateSystem.IsDead(uid))
                return false;

            var attemptSneezeCoughEvent = new AttemptSneezeCoughEvent(emoteId);
            RaiseLocalEvent(uid, attemptSneezeCoughEvent);
            if (attemptSneezeCoughEvent.Cancelled)
                return false;

            _chatSystem.TryEmoteWithChat(uid, emoteId);

            var disease = _prototypeManager.Index(diseaseId);

            if (disease is not { Infectious: true } || !airTransmit)
                return true;

            if (_inventorySystem.TryGetSlotEntity(uid, "mask", out var maskUid) &&
                TryComp<IngestionBlockerComponent>(maskUid, out var blocker) &&
                blocker.Enabled)
                return true;

            QueueLocalEvent(new DiseaseInfectionSpreadEvent
            {
                Owner = uid,
                Disease = disease,
                Range = 2f
            });

            return true;
        }
}

/// <summary>
/// This event is fired by chems
/// and other brute-force rather than
/// specific cures. It will roll the dice to attempt
/// to cure each disease on the target
/// </summary>
public sealed class CureDiseaseAttemptEvent(float cureChance) : EntityEventArgs
{
    public float CureChance { get; } = cureChance;
}

/// <summary>
/// Controls whether the snough is a sneeze, cough
/// or neither. If none, will not create
/// a popup. Mostly used for talking
/// </summary>
public enum SneezeCoughType
{
    Sneeze,
    Cough,
    None
}
