using System.Linq;
using Content.Server.Antag;
using Content.Shared.Verbs;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Administration.Logs;
using Content.Shared.Backmen.Vampiric;
using Content.Server.Atmos.Components;
using Content.Server.Backmen.Surgery.Wounds.Systems;
using Content.Server.Backmen.Vampiric.Role;
using Content.Server.Backmen.Vampiric.Rule;
using Content.Server.Bible.Components;
using Content.Shared.Body.Components;
using Content.Server.Popups;
using Content.Server.DoAfter;
using Content.Server.Mind;
using Content.Server.NPC.Components;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Surgery.Pain;
using Content.Shared.Backmen.Surgery.Pain.Systems;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Backmen.Vampiric.Components;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage.Systems;
using Content.Server.Backmen.Cocoon;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Content.Shared.Forensics.Components;
using Content.Shared.Forensics.Systems;
using Content.Shared.HealthExaminable;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Roles;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Vampiric;

public sealed partial class BloodSuckerSystem : SharedBloodSuckerSystem
{
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private BkmBodySharedSystem _bodySystem = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionSystem = default!;
    [Dependency] private PopupSystem _popups = default!;
    [Dependency] private DoAfterSystem _doAfter = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private StomachSystem _stomachSystem = default!;
    [Dependency] private InventorySystem _inventorySystem = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private ReactiveSystem _reactiveSystem = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private HungerSystem _hunger = default!;
    [Dependency] private SharedRoleSystem _roleSystem = default!;
    [Dependency] private MindSystem _mindSystem = default!;
    [Dependency] private BkmVampireLevelingSystem _leveling = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private SharedCuffableSystem _cuffableSystem = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ServerWoundSystem _wound = default!;
    [Dependency] private PainSystem _pain = default!;
    [Dependency] private ConsciousnessSystem _consciousness = default!;
    [Dependency] private SharedPuddleSystem _puddle = default!;
    [Dependency] private SharedForensicsSystem _forensics = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    private EntityQuery<BloodSuckerComponent> _bsQuery;

    private readonly EntProtoId BloodsuckerMindRole = "MindRoleBloodsucker";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetVerbsEvent<AlternativeVerb>>(AddSuccVerb);

        SubscribeLocalEvent<BloodSuckedComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<BloodSuckedComponent, WoundsChangedEvent>(OnWoundsChanged);
        SubscribeLocalEvent<BloodSuckerComponent, BloodSuckDoAfterEvent>(OnDoAfter);


        SubscribeLocalEvent<BkmVampireComponent, MapInitEvent>(OnInitVmp);

        SubscribeLocalEvent<BkmVampireComponent, PlayerAttachedEvent>(OnAttachedVampireMind);
        SubscribeLocalEvent<BkmVampireComponent, HealthBeingExaminedEvent>(OnVampireExamined);

        _bsQuery = GetEntityQuery<BloodSuckerComponent>();
    }

    private void OnInitVmp(Entity<BkmVampireComponent> ent, ref MapInitEvent args)
    {
        _leveling.InitShop(ent);
    }

    private void OnVampireExamined(Entity<BkmVampireComponent> ent, ref HealthBeingExaminedEvent args)
    {
        if (!_hunger.IsHungerBelowState(ent, HungerThreshold.Okay))
            return;

        args.Message.PushNewline();
        args.Message.TryAddMarkup(Loc.GetString("vampire-health-examine", ("target", ent.Owner)), out _);
    }

    private void OnAttachedVampireMind(Entity<BkmVampireComponent> ent, ref PlayerAttachedEvent args)
    {
        EnsureMindVampire(ent);
    }

    private readonly EntProtoId OrganVampiricHumanoidStomach = "OrganVampiricHumanoidStomach";
    private readonly EntProtoId DefaultVampireRule = "VampiresGameRule";

    public void ConvertToVampire(EntityUid uid)
    {
        if (
            _bsQuery.HasComp(uid) ||
            !HasComp<BodyComponent>(uid)
            || !CanBeSucked(uid)
            )
            return;

        EnsureComp<BloodSuckerComponent>(uid);

        if (!TryComp<BodyComponent>(uid, out var bodyComp))
            return;

        foreach (var organ in _bodySystem.GetBodyOrganEntityComps<StomachComponent>((uid, bodyComp)))
        {
            _bodySystem.RemoveOrgan(organ.Owner, organ.Comp2);

            var stomach = Spawn(OrganVampiricHumanoidStomach);
            _bodySystem.InsertOrganIntoBody(uid, stomach);

            QueueDel(organ.Owner);
        }

        EnsureComp<BkmVampireComponent>(uid);

        if (
            TryComp<BloodSuckedComponent>(uid, out var bloodsucked) &&
            bloodsucked.BloodSuckerMindId.HasValue &&
            !TerminatingOrDeleted(bloodsucked.BloodSuckerMindId.Value) &&
            _roleSystem.MindHasRole<VampireRoleComponent>(bloodsucked.BloodSuckerMindId.Value, out var bloodsucker)
            )
        {
            var masterUid = CompOrNull<MindComponent>(bloodsucked.BloodSuckerMindId.Value)?.CurrentEntity;
            if (TryComp<BkmVampireComponent>(masterUid, out var master))
            {
                _leveling.AddCurrency((masterUid.Value,master),
                    10 * (bloodsucker.Value.Comp2.Tier + 1),
                    "обращение"
                    );
            }


            bloodsucker.Value.Comp2.Converted += 1;
        }

        EnsureMindVampire(uid);
    }

    public void EnsureMindVampire(EntityUid uid)
    {
        if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
        {
            return; // no mind? skip;
        }

        if (_roleSystem.MindHasRole<VampireRoleComponent>(mindId))
        {
            return; // have it
        }

        _roleSystem.MindAddRole(mindId, BloodsuckerMindRole, mind, true);
    }

    public void MakeVampire(EntityUid uid, bool isElder = false)
    {
        _mindSystem.TryGetMind(uid, out var mindId, out _);
        ConvertToVampire(uid);
        var vmpRule = EntityQuery<BloodsuckerRuleComponent>().FirstOrDefault();

        if (vmpRule == null)
            return;

        if (isElder)
            vmpRule.Elders.TryAdd(Name(uid), mindId);

        vmpRule.TotalBloodsuckers++;
    }

    public override void ForceMakeVampire(EntityUid uid)
    {
        if (!TryComp<ActorComponent>(uid, out var actor) || !CanBeSucked(uid))
            return;

        if (_roleSystem.MindHasRole<VampireRoleComponent>(uid, out _))
            return;

        _antag.ForceMakeAntag<BloodsuckerRuleComponent>(actor.PlayerSession, DefaultVampireRule);
    }

    private void AddSuccVerb(GetVerbsEvent<AlternativeVerb> ev)
    {
        if (
            !ev.CanAccess ||
            !ev.CanInteract ||
            ev.User == ev.Target ||
            !_bsQuery.TryComp(ev.User, out var component)
            )
            return;

        if (component.WebRequired)
            return; // handled elsewhere

        if (!TryComp<BloodstreamComponent>(ev.Target, out var bloodstream))
            return;

        AlternativeVerb verb = new()
        {
            Act = () =>
            {
                StartSuccDoAfter(ev.User, ev.Target, component, bloodstream); // start doafter
            },
            Text = Loc.GetString("action-name-suck-blood"),
            Icon = new SpriteSpecifier.Texture(new ("/Textures/Backmen/Icons/verbiconfangs.png")),
            Priority = 2
        };
        ev.Verbs.Add(verb);
    }

    private static readonly ProtoId<DamageGroupPrototype> Brute = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> Airloss = "Airloss";

    private void OnDamageChanged(EntityUid uid, BloodSuckedComponent component, DamageChangedEvent args)
    {
        if (args.DamageIncreased)
            return;

        if (_prototypeManager.TryIndex<DamageGroupPrototype>(Brute, out var brute)
            && _damageableSystem.GetDamagePerGroup((uid, args.Damageable)).TryGetValue(brute, out var bruteTotal)
            && _prototypeManager.TryIndex<DamageGroupPrototype>(Airloss, out var airloss)
            && _damageableSystem.GetDamagePerGroup((uid, args.Damageable)).TryGetValue(airloss, out var airlossTotal))
        {
            if (bruteTotal == 0 && airlossTotal == 0)
                RemComp<BloodSuckedComponent>(uid);
        }
    }

    private void OnWoundsChanged(EntityUid uid, BloodSuckedComponent component, WoundsChangedEvent args)
    {
        if (args.DamageIncreased)
            return;

        if (!_consciousness.CheckConscious(uid))
            return;

        var damagePresent =
            _wound.GetBodyWounds(uid).Any(wound => wound.Comp.DamageGroup?.ID == "Brute");

        if (!damagePresent)
            RemComp<BloodSuckedComponent>(uid);
    }

    private void OnDoAfter(EntityUid uid, BloodSuckerComponent component, BloodSuckDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        if (!TryGetSuccTarget(args.Args.Target.Value, out var victim))
            return;

        args.Handled = TrySucc(uid, victim);
    }

    private static readonly ProtoId<ReagentPrototype> Blood = "Blood";
    public bool CanBeSucked(Entity<BloodstreamComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp) || ent.Comp.BloodReferenceSolution is not {} bloodReferenceSolution)
            return false;

        if(HasComp<BibleUserComponent>(ent))
            return false;

        return bloodReferenceSolution.ContainsPrototype(Blood) && ent.Comp.BloodSolution != null;
    }

    public bool TryRetaliate(Entity<NPCRetaliationComponent> ent, EntityUid target)
    {
        // don't retaliate against inanimate objects.
        if (!HasComp<MobStateComponent>(target))
            return false;

        _npcFaction.AggroEntity(ent.Owner, target);
        if (ent.Comp.AttackMemoryLength is {} memoryLength)
            ent.Comp.AttackMemories[target] = _timing.CurTime + memoryLength;

        return true;
    }

    public bool StartSuccDoAfter(EntityUid bloodsucker, EntityUid victim, BloodSuckerComponent? bloodSuckerComponent = null, BloodstreamComponent? stream = null, bool doChecks = true, EntityUid? doAfterTarget = null)
    {
        if (!Resolve(bloodsucker, ref bloodSuckerComponent))
            return false;

        if (!Resolve(victim, ref stream) || stream.BloodReferenceSolution is not {} bloodReferenceSolution)
            return false;

        var interactTarget = doAfterTarget ?? victim;

        if (doChecks)
        {
            if (!_interactionSystem.InRangeUnobstructed(bloodsucker, interactTarget))
            {
                return false;
            }

            if (_inventorySystem.TryGetSlotEntity(victim, "head", out var headUid) && HasComp<PressureProtectionComponent>(headUid))
            {
                _popups.PopupEntity(Loc.GetString("bloodsucker-fail-helmet", ("helmet", headUid)), victim, bloodsucker, Shared.Popups.PopupType.Medium);
                return false;
            }

            if (_inventorySystem.TryGetSlotEntity(bloodsucker, "mask", out var maskUid) &&
                TryComp<IngestionBlockerComponent>(maskUid, out var blocker) &&
                blocker.Enabled)
            {
                _popups.PopupEntity(Loc.GetString("bloodsucker-fail-mask", ("mask", maskUid)), victim, bloodsucker, Shared.Popups.PopupType.Medium);
                return false;
            }
        }

        if (!CanBeSucked((victim,stream)))
        {
            _popups.PopupEntity(Loc.GetString("bloodsucker-fail-not-blood", ("target", victim)), victim, bloodsucker, Shared.Popups.PopupType.Medium);
            return false;
        }

        if (!TryGetDrainableBloodVolume((victim, stream), out var drainableVolume) || drainableVolume <= 1)
        {
            if (HasComp<BloodSuckedComponent>(victim))
                _popups.PopupEntity(Loc.GetString("bloodsucker-fail-no-blood-bloodsucked", ("target", victim)), victim, bloodsucker, Shared.Popups.PopupType.Medium);
            else
                _popups.PopupEntity(Loc.GetString("bloodsucker-fail-no-blood", ("target", victim)), victim, bloodsucker, Shared.Popups.PopupType.Medium);

            return false;
        }

        var ev = new BloodSuckDoAfterEvent();
        var args = new DoAfterArgs(EntityManager, bloodsucker, bloodSuckerComponent.SuccDelay, ev, bloodsucker, target: interactTarget)
        {
            BreakOnMove = true,
            DistanceThreshold = 2f,
            NeedHand = false
        };

        if (!_doAfter.TryStartDoAfter(args))
            return false;

        _popups.PopupEntity(Loc.GetString("bloodsucker-doafter-start-victim", ("sucker", bloodsucker)), victim, victim, Shared.Popups.PopupType.LargeCaution);
        _popups.PopupEntity(Loc.GetString("bloodsucker-doafter-start", ("target", victim)), victim, bloodsucker, Shared.Popups.PopupType.Medium);

        if (TryComp<NPCRetaliationComponent>(victim, out var npcRetaliationComponent))
        {
            if (TryComp<CuffableComponent>(victim, out var victimCuff) && _cuffableSystem.IsCuffed((victim, victimCuff)))
            {
                _cuffableSystem.TryUncuff(victim, bloodsucker);
            }
            TryRetaliate((victim, npcRetaliationComponent), bloodsucker);
        }

        return true;
    }

    [ViewVariables(VVAccess.ReadWrite)]
    public float BasePoints = 1f;

    public bool TrySucc(EntityUid bloodsucker, EntityUid victim, BloodSuckerComponent? bloodsuckerComp = null, BloodstreamComponent? victimBloodstream = null)
    {
        // Is bloodsucker a bloodsucker?
        if (!_bsQuery.Resolve(bloodsucker, ref bloodsuckerComp))
            return false;

        // Does victim have a bloodstream?
        if (!Resolve(victim, ref victimBloodstream)
            || victimBloodstream.BloodSolution is not {} victimBloodSolution
            || victimBloodstream.BloodReferenceSolution is not {} bloodReferenceSolution)
            return false;


        var victimBloodVolume = victimBloodSolution.Comp.Solution.GetTotalPrototypeQuantity(Blood);

        if (victimBloodVolume <= 1)
        {
            _popups.PopupEntity(Loc.GetString("drink-component-try-use-drink-had-enough"), bloodsucker, bloodsucker, Shared.Popups.PopupType.MediumCaution);
            return false;
        }

        if (!_bodySystem.TryGetBodyOrganEntityComps<StomachComponent>((bloodsucker, null), out var stomachs) ||
            stomachs.Count == 0)
        {
            return false;
        }

        var suckerStomach = stomachs[0];

        if (!_solutionSystem.ResolveSolution(suckerStomach.Owner, StomachSystem.DefaultSolutionName, ref suckerStomach.Comp1.Solution, out var suckerStomachSolution))
            return false;

        var unitsToDrain = Math.Min(victimBloodVolume.Float(), bloodsuckerComp.UnitsToSucc);
        var suckerStomachAvailableVolume = suckerStomachSolution.AvailableVolume;

        if (suckerStomachAvailableVolume < unitsToDrain)
            unitsToDrain = suckerStomachAvailableVolume.Float();

        if (unitsToDrain <= 2)
        {
            _popups.PopupEntity(Loc.GetString("drink-component-try-use-drink-had-enough"), bloodsucker, bloodsucker, Shared.Popups.PopupType.MediumCaution);
            return false;
        }

        var temp = victimBloodSolution.Comp.Solution.SplitSolutionWithOnly(unitsToDrain, Blood);
        _solutionSystem.UpdateChemicals(victimBloodSolution);

        if (temp.Volume <= 0)
            return false;

        _reactiveSystem.DoEntityReaction(bloodsucker, temp, ReactionMethod.Ingestion);

        if (!_stomachSystem.TryTransferSolution((suckerStomach.Owner, suckerStomach), temp))
        {
            _solutionSystem.TryAddSolution(victimBloodSolution, temp);
            return false;
        }

        _adminLogger.Add(Shared.Database.LogType.MeleeHit, Shared.Database.LogImpact.Medium, $"{ToPrettyString(bloodsucker):player} sucked blood from {ToPrettyString(victim):target}");

        _audio.PlayPvs("/Audio/Items/drink.ogg", bloodsucker);
        _popups.PopupEntity(Loc.GetString("bloodsucker-blood-sucked-victim", ("sucker", bloodsucker)), victim, victim, Shared.Popups.PopupType.LargeCaution);
        var doNotify = true;

        if (_mindSystem.TryGetMind(bloodsucker, out var bloodSuckerMindId, out _))
        {
            EnsureComp<BloodSuckedComponent>(victim).BloodSuckerMindId = bloodSuckerMindId;

            if (_roleSystem.MindHasRole<VampireRoleComponent>(bloodSuckerMindId, out var role))
            {
                var vpm = role.Value.Comp2;
                vpm.Drink += unitsToDrain;

                if (TryComp<BkmVampireComponent>(bloodsucker, out var bkmVampireComponent))
                {
                    _leveling.AddCurrency((bloodsucker,bkmVampireComponent),
                        (
                            BasePoints *
                            MathF.Pow(1.5f, vpm.Tier) *
                            BloodPrice(
                                (bloodsucker, bkmVampireComponent),
                                victim,
                                unitsToDrain
                            )
                        ), "укус");
                    doNotify = false;
                }
            }
        }
        else
        {
            EnsureComp<BloodSuckedComponent>(victim).BloodSuckerMindId = null;
        }

        if (doNotify)
            _popups.PopupEntity(Loc.GetString("bloodsucker-blood-sucked", ("target", victim)), bloodsucker, bloodsucker, Shared.Popups.PopupType.Medium);

        if (_consciousness.TryGetNerveSystem(victim, out var nerve))
        {
            _pain.TryChangePainModifier(nerve.Value, nerve.Value.Comp.RootNerve, "Bloodsucker", 10, nerve.Value, TimeSpan.FromMinutes(1), PainType.TraumaticPain);
        }



        // Add a little pierce
        DamageSpecifier damage = new();
        damage.DamageDict.Add("Piercing", 10); // Slowly accumulate enough to gib after like half an hour

        _damageableSystem.ChangeDamage(victim, damage, true, true, origin: bloodsucker, targetPart: TargetBodyPart.Head);


        if (bloodsuckerComp.InjectWhenSucc)
        {
            _solutionSystem.TryAddReagent(victimBloodSolution, bloodsuckerComp.InjectReagent, bloodsuckerComp.UnitsToInject, out var acceptedQuantity);
        }
        _npcFaction.AggroEntity(victim, bloodsucker);

        return true;
    }

    [ViewVariables(VVAccess.ReadWrite)]
    public float SuckFromBloodSucker = 0.75f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float SuckFromNoneDna = 0.5f;
    [ViewVariables(VVAccess.ReadWrite)]
    public float SuckDnaPenaltyFrom1 = 0.85f;
    [ViewVariables(VVAccess.ReadWrite)]
    public float SuckDnaMaxPenalty = 0.2f;
    [ViewVariables(VVAccess.ReadWrite)]
    public float SuckUnitDivisionCoef = 15f;
    /// <summary>
    /// Resolve multiplier
    /// </summary>
    private float BloodPrice(Entity<BkmVampireComponent> vamp, EntityUid victim, float unitsToDrain)
    {
        float pr = 1f;

        // Штраф за питьё у других вампиров (-75%)
        if (HasComp<BloodSuckerComponent>(victim))
        {
            pr -= SuckFromBloodSucker;
        }

        // Штраф за отсутствие ДНК (-50% вместо -80%)
        if (!TryComp<DnaComponent>(victim, out var dnaComponent) || string.IsNullOrEmpty(dnaComponent.DNA))
        {
            pr -= SuckFromNoneDna;
        }
        else
        {
            vamp.Comp.DNA.TryAdd(dnaComponent.DNA, 0);

            // Новый расчёт: экспоненциальный штраф за каждые 5 единиц крови
            float bloodDrained = vamp.Comp.DNA[dnaComponent.DNA].Float();
            float dnaPenalty = MathF.Pow(SuckDnaPenaltyFrom1, bloodDrained / 5f); // Каждые 5 ед. -15%
            pr *= dnaPenalty;

            // Обновляем счётчик ДНК
            vamp.Comp.DNA[dnaComponent.DNA] += unitsToDrain;
        }

        // Максимальный штраф за ДНК (не менее 20% от базового)
        pr = Math.Max(pr, SuckDnaMaxPenalty);

        // Учёт объёма крови
        pr *= unitsToDrain / SuckUnitDivisionCoef;

        return Math.Max(0f, pr);
    }

    private const float MinStomachBlood = 30f;
    public const string FailedBloodPuddlesKey = "FailedBloodPuddles";
    public const string FailedCocoonMealsKey = "FailedCocoonMeals";
    public const float MaxPuddleSeekRange = 7f;

    public bool NeedsBlood(EntityUid uid)
    {
        var stomach = _bodySystem.GetBodyOrganEntityComps<StomachComponent>(uid).FirstOrNull();

        if (stomach != null &&
            _solutionSystem.TryGetSolution(stomach.Value.Owner, StomachSystem.DefaultSolutionName, out var sol))
        {
            var solution = sol.Value.Comp.Solution;

            if (solution.GetTotalPrototypeQuantity(Blood) >= MinStomachBlood / 2)
                return false;
        }

        if (TryComp<HungerComponent>(uid, out var hunger) &&
            hunger.CurrentThreshold < HungerThreshold.Okay)
        {
            return true;
        }

        if (TryComp<ThirstComponent>(uid, out var thirst) &&
            thirst.CurrentThirstThreshold < ThirstThreshold.Okay)
        {
            return true;
        }

        if (stomach == null)
            return true;

        if (!_solutionSystem.TryGetSolution(stomach.Value.Owner, StomachSystem.DefaultSolutionName, out sol))
            return true;

        return sol.Value.Comp.Solution.Volume < sol.Value.Comp.Solution.MaxVolume - MinStomachBlood;
    }

    private bool TryGetDrainableBloodVolume(Entity<BloodstreamComponent?> ent, out FixedPoint2 volume)
    {
        volume = FixedPoint2.Zero;

        if (!Resolve(ent, ref ent.Comp) || ent.Comp.BloodSolution is not {} bloodSolution)
            return false;

        volume = bloodSolution.Comp.Solution.GetTotalPrototypeQuantity(Blood);
        return true;
    }

    public bool IsInCocoon(EntityUid victim)
    {
        if (_container.TryGetContainingContainer(victim, out var container) &&
            HasComp<CocoonComponent>(container.Owner))
        {
            return true;
        }

        // Victims are stored in the cocoon body_slot; container lookup alone can miss them.
        var parent = Transform(victim).ParentUid;
        return parent.IsValid() &&
               HasComp<CocoonComponent>(parent) &&
               _itemSlots.GetItemOrNull(parent, CocoonerSystem.BodySlot) == victim;
    }

    public bool TryGetSuccTarget(EntityUid target, out EntityUid victim)
    {
        victim = target;

        if (!HasComp<CocoonComponent>(target))
            return true;

        var body = _itemSlots.GetItemOrNull(target, CocoonerSystem.BodySlot);
        if (body == null)
            return false;

        victim = body.Value;
        return true;
    }

    public bool PuddleHasBlood(EntityUid puddle)
    {
        if (!TryComp<PuddleComponent>(puddle, out var puddleComp))
            return false;

        if (!_solutionSystem.TryGetSolution(puddle, puddleComp.SolutionName, out _, out var sol))
            return false;

        return sol.Volume > 5 && sol.ContainsPrototype(Blood);
    }

    public bool CanDrinkBloodPuddle(EntityUid bloodsucker, EntityUid puddle, bool checkRange = true, BloodSuckerComponent? bloodSuckerComponent = null)
    {
        if (!Resolve(bloodsucker, ref bloodSuckerComponent) || !NeedsBlood(bloodsucker) || !PuddleHasBlood(puddle))
            return false;

        if (checkRange && !_interactionSystem.InRangeUnobstructed(bloodsucker, puddle, SharedInteractionSystem.InteractionRange))
            return false;

        if (!TryComp<PuddleComponent>(puddle, out var puddleComp) ||
            !_solutionSystem.TryGetSolution(puddle, puddleComp.SolutionName, out var puddleSolution))
        {
            return false;
        }

        var stomach = _bodySystem.GetBodyOrganEntityComps<StomachComponent>(bloodsucker).FirstOrNull();
        if (stomach == null)
            return false;

        if (!_solutionSystem.TryGetSolution(stomach.Value.Owner, StomachSystem.DefaultSolutionName, out var suckerStomachSolution))
            return false;

        var available = suckerStomachSolution.Value.Comp.Solution.AvailableVolume;
        if (available <= 2)
            return false;

        var bloodVolume = puddleSolution.Value.Comp.Solution.GetTotalPrototypeQuantity(Blood);
        var units = Math.Min(Math.Min(bloodVolume.Float(), bloodSuckerComponent.UnitsToSucc), available.Float());

        return units > 2;
    }

    public bool TryDrinkBloodPuddle(EntityUid bloodsucker, EntityUid puddle, BloodSuckerComponent? bloodSuckerComponent = null)
    {
        if (!CanDrinkBloodPuddle(bloodsucker, puddle, true, bloodSuckerComponent))
            return false;

        if (!Resolve(bloodsucker, ref bloodSuckerComponent) || !PuddleHasBlood(puddle))
            return false;

        if (!TryComp<PuddleComponent>(puddle, out var puddleComp) ||
            !_solutionSystem.TryGetSolution(puddle, puddleComp.SolutionName, out var puddleSolution))
        {
            return false;
        }

        var stomach = _bodySystem.GetBodyOrganEntityComps<StomachComponent>(bloodsucker).FirstOrNull();
        if (stomach == null)
            return false;

        if (!_solutionSystem.TryGetSolution(stomach.Value.Owner, StomachSystem.DefaultSolutionName, out var suckerStomachSolution))
            return false;

        var available = suckerStomachSolution.Value.Comp.Solution.AvailableVolume;
        if (available <= 2)
            return false;

        var bloodVolume = puddleSolution.Value.Comp.Solution.GetTotalPrototypeQuantity(Blood);
        var units = Math.Min(Math.Min(bloodVolume.Float(), bloodSuckerComponent.UnitsToSucc), available.Float());

        if (units <= 2)
            return false;

        var temp = puddleSolution.Value.Comp.Solution.SplitSolutionWithOnly(units, Blood);
        _solutionSystem.UpdateChemicals(puddleSolution.Value);
        _reactiveSystem.DoEntityReaction(bloodsucker, temp, ReactionMethod.Ingestion);

        if (!_stomachSystem.TryTransferSolution((stomach.Value.Owner, stomach.Value), temp))
            return false;

        _audio.PlayPvs("/Audio/Items/drink.ogg", bloodsucker);
        return true;
    }

    public bool HasDrinkableCocoonMeal(EntityUid bloodsucker, EntityUid target, bool checkRange = true, BloodSuckerComponent? bloodSuckerComponent = null)
    {
        if (!Resolve(bloodsucker, ref bloodSuckerComponent) || !NeedsBlood(bloodsucker) || !HasComp<CocoonComponent>(target))
            return false;

        if (!TryGetSuccTarget(target, out var victim))
            return false;

        if (!TryComp<BloodstreamComponent>(victim, out var stream))
            return false;

        if (!CanBeSucked((victim, stream)))
            return false;

        if (!TryGetDrainableBloodVolume((victim, stream), out var drainableVolume) || drainableVolume <= 1)
            return false;

        var stomach = _bodySystem.GetBodyOrganEntityComps<StomachComponent>(bloodsucker).FirstOrNull();
        if (stomach == null ||
            !_solutionSystem.TryGetSolution(stomach.Value.Owner, StomachSystem.DefaultSolutionName, out var suckerStomachSolution))
        {
            return false;
        }

        var units = Math.Min(drainableVolume.Float(), bloodSuckerComponent.UnitsToSucc);
        units = Math.Min(units, suckerStomachSolution.Value.Comp.Solution.AvailableVolume.Float());

        if (units <= 2)
            return false;

        if (!checkRange)
            return true;

        return _interactionSystem.InRangeUnobstructed(bloodsucker, target);
    }

    public bool HasNearbyDrinkableCocoonMeals(
        EntityUid bloodsucker,
        float range,
        HashSet<EntityUid>? failedMeals = null,
        BloodSuckerComponent? bloodSuckerComponent = null)
    {
        if (!Resolve(bloodsucker, ref bloodSuckerComponent) || !NeedsBlood(bloodsucker))
            return false;

        foreach (var ent in _lookup.GetEntitiesInRange(bloodsucker, range))
        {
            if (!HasComp<CocoonComponent>(ent))
                continue;

            if (failedMeals != null && failedMeals.Contains(ent))
                continue;

            if (HasDrinkableCocoonMeal(bloodsucker, ent, checkRange: false, bloodSuckerComponent))
                return true;
        }

        return false;
    }

    public bool CanSucc(EntityUid bloodsucker, EntityUid target, bool checkRange = true, BloodSuckerComponent? bloodSuckerComponent = null)
    {
        if (!Resolve(bloodsucker, ref bloodSuckerComponent))
            return false;

        if (!TryGetSuccTarget(target, out var victim))
            return false;

        if (!_mobState.IsAliveOrSoftCrit(victim))
            return false;

        if (!TryComp<BloodstreamComponent>(victim, out var stream))
            return false;

        if (bloodSuckerComponent.WebRequired && !HasComp<CocoonComponent>(target) && !IsInCocoon(victim))
            return false;

        if (!CanBeSucked((victim, stream)))
            return false;

        if (!TryGetDrainableBloodVolume((victim, stream), out var drainableVolume) || drainableVolume <= 1)
            return false;

        if (!checkRange)
            return true;

        return _interactionSystem.InRangeUnobstructed(bloodsucker, HasComp<CocoonComponent>(target) ? target : victim);
    }

    public bool NPCStartSucc(EntityUid bloodsucker, EntityUid target, BloodSuckerComponent? bloodSuckerComponent = null)
    {
        if (!Resolve(bloodsucker, ref bloodSuckerComponent))
            return false;

        // MoveTo already brought the NPC close; match the manual cocoon verb (doChecks: false).
        if (HasComp<CocoonComponent>(target))
        {
            if (!HasDrinkableCocoonMeal(bloodsucker, target, checkRange: false, bloodSuckerComponent))
                return false;
        }
        else if (!CanSucc(bloodsucker, target, bloodSuckerComponent: bloodSuckerComponent))
        {
            return false;
        }

        if (!TryGetSuccTarget(target, out var victim) ||
            !TryComp<BloodstreamComponent>(victim, out var stream))
        {
            return false;
        }

        var doAfterTarget = HasComp<CocoonComponent>(target) ? target : victim;
        return StartSuccDoAfter(bloodsucker, victim, bloodSuckerComponent, stream, false, doAfterTarget);
    }
}
