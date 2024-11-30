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
using Content.Server.Backmen.Vampiric.Role;
using Content.Server.Backmen.Vampiric.Rule;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Popups;
using Content.Server.DoAfter;
using Content.Server.Forensics;
using Content.Server.Mind;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server.Nutrition.Components;
using Content.Shared.Backmen.Vampiric.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.HealthExaminable;
using Content.Shared.Mind;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Vampiric;

public sealed class BloodSuckerSystem : SharedBloodSuckerSystem
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly BodySystem _bodySystem = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionSystem = default!;
    [Dependency] private readonly PopupSystem _popups = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly StomachSystem _stomachSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private readonly ReactiveSystem _reactiveSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly SharedRoleSystem _roleSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly BkmVampireLevelingSystem _leveling = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly NPCRetaliationSystem _retaliationSystem = default!;
    private EntityQuery<BloodSuckerComponent> _bsQuery;

    [ValidatePrototypeId<EntityPrototype>] private const string BloodsuckerMindRole = "MindRoleBloodsucker";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetVerbsEvent<AlternativeVerb>>(AddSuccVerb);

        SubscribeLocalEvent<BloodSuckedComponent, DamageChangedEvent>(OnDamageChanged);
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

    [ValidatePrototypeId<EntityPrototype>]
    private const string OrganVampiricHumanoidStomach = "OrganVampiricHumanoidStomach";

    [ValidatePrototypeId<EntityPrototype>]
    private const string DefaultVampireRule = "VampiresGameRule";

    public void ConvertToVampire(EntityUid uid)
    {
        if (
            _bsQuery.HasComp(uid) ||
            !TryComp<BodyComponent>(uid, out var bodyComponent) ||
            !TryComp<BodyPartComponent>(bodyComponent.RootContainer.ContainedEntity, out var bodyPartComponent)
            )
            return;

        EnsureComp<BloodSuckerComponent>(uid);

        var stomachs = _bodySystem.GetBodyOrganEntityComps<StomachComponent>(uid);// GetBodyOrganComponents<StomachComponent>(uid);
        foreach (var organId in stomachs)
        {
            _bodySystem.RemoveOrgan(organId,organId);
            QueueDel(organId);
        }

        var stomach = Spawn(OrganVampiricHumanoidStomach);

        _bodySystem.InsertOrgan(bodyComponent.RootContainer.ContainedEntity.Value, stomach, "stomach", bodyPartComponent);

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
            vmpRule.Elders.Add(Name(uid), mindId);

        vmpRule.TotalBloodsuckers++;
    }

    public void ForceMakeVampire(EntityUid uid)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        _antag.ForceMakeAntag<BloodsuckerRuleComponent>(actor.PlayerSession, DefaultVampireRule);
    }

    private void AddSuccVerb(GetVerbsEvent<AlternativeVerb> ev)
    {
        if (
            !ev.CanAccess ||
            !ev.CanInteract ||
            ev.User == ev.Target ||
            IsClientSide(ev.Target) ||
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

    private void OnDamageChanged(EntityUid uid, BloodSuckedComponent component, DamageChangedEvent args)
    {
        if (args.DamageIncreased)
            return;

        if (_prototypeManager.TryIndex<DamageGroupPrototype>("Brute", out var brute) && args.Damageable.Damage.TryGetDamageInGroup(brute, out var bruteTotal)
            && _prototypeManager.TryIndex<DamageGroupPrototype>("Airloss", out var airloss) && args.Damageable.Damage.TryGetDamageInGroup(airloss, out var airlossTotal))
        {
            if (bruteTotal == 0 && airlossTotal == 0)
                RemComp<BloodSuckedComponent>(uid);
        }
    }

    private void OnDoAfter(EntityUid uid, BloodSuckerComponent component, BloodSuckDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        args.Handled = TrySucc(uid, args.Args.Target.Value);
    }

    public void StartSuccDoAfter(EntityUid bloodsucker, EntityUid victim, BloodSuckerComponent? bloodSuckerComponent = null, BloodstreamComponent? stream = null, bool doChecks = true)
    {
        if (!Resolve(bloodsucker, ref bloodSuckerComponent))
            return;

        if (!Resolve(victim, ref stream))
            return;

        if (doChecks)
        {
            if (!_interactionSystem.InRangeUnobstructed(bloodsucker, victim))
            {
                return;
            }

            if (_inventorySystem.TryGetSlotEntity(victim, "head", out var headUid) && HasComp<PressureProtectionComponent>(headUid))
            {
                _popups.PopupEntity(Loc.GetString("bloodsucker-fail-helmet", ("helmet", headUid)), victim, bloodsucker, Shared.Popups.PopupType.Medium);
                return;
            }

            if (_inventorySystem.TryGetSlotEntity(bloodsucker, "mask", out var maskUid) &&
                EntityManager.TryGetComponent<IngestionBlockerComponent>(maskUid, out var blocker) &&
                blocker.Enabled)
            {
                _popups.PopupEntity(Loc.GetString("bloodsucker-fail-mask", ("mask", maskUid)), victim, bloodsucker, Shared.Popups.PopupType.Medium);
                return;
            }
        }

        if (stream.BloodReagent != "Blood" || stream.BloodSolution == null)
        {
            _popups.PopupEntity(Loc.GetString("bloodsucker-fail-not-blood", ("target", victim)), victim, bloodsucker, Shared.Popups.PopupType.Medium);
            return;
        }

        if (stream.BloodSolution.Value.Comp.Solution.Volume <= 1)
        {
            if (HasComp<BloodSuckedComponent>(victim))
                _popups.PopupEntity(Loc.GetString("bloodsucker-fail-no-blood-bloodsucked", ("target", victim)), victim, bloodsucker, Shared.Popups.PopupType.Medium);
            else
                _popups.PopupEntity(Loc.GetString("bloodsucker-fail-no-blood", ("target", victim)), victim, bloodsucker, Shared.Popups.PopupType.Medium);

            return;
        }

        _popups.PopupEntity(Loc.GetString("bloodsucker-doafter-start-victim", ("sucker", bloodsucker)), victim, victim, Shared.Popups.PopupType.LargeCaution);
        _popups.PopupEntity(Loc.GetString("bloodsucker-doafter-start", ("target", victim)), victim, bloodsucker, Shared.Popups.PopupType.Medium);

        if (TryComp<NPCRetaliationComponent>(victim, out var npcRetaliationComponent))
        {
            _retaliationSystem.TryRetaliate((victim, npcRetaliationComponent), bloodsucker);
        }

        var ev = new BloodSuckDoAfterEvent();
        var args = new DoAfterArgs(EntityManager, bloodsucker, bloodSuckerComponent.SuccDelay, ev, bloodsucker, target: victim)
        {
            BreakOnMove = true,
            DistanceThreshold = 2f,
            NeedHand = false
        };

        _doAfter.TryStartDoAfter(args);
    }

    public bool TrySucc(EntityUid bloodsucker, EntityUid victim, BloodSuckerComponent? bloodsuckerComp = null, BloodstreamComponent? bloodstream = null)
    {
        // Is bloodsucker a bloodsucker?
        if (!_bsQuery.Resolve(bloodsucker, ref bloodsuckerComp))
            return false;

        // Does victim have a bloodstream?
        if (!Resolve(victim, ref bloodstream))
            return false;

        // No blood left, yikes.
        if (bloodstream.BloodSolution == null || bloodstream.BloodSolution.Value.Comp.Solution.Volume == 0)
            return false;

        var bloodstreamVolume = bloodstream.BloodSolution!.Value.Comp.Solution.Volume;

        // Does bloodsucker have a stomach?
        var stomachList = _bodySystem.GetBodyOrganEntityComps<StomachComponent>(bloodsucker).FirstOrNull();
        if (stomachList == null)
            return false;

        if (!_solutionSystem.TryGetSolution(stomachList.Value.Owner, StomachSystem.DefaultSolutionName, out var stomachSolution))
            return false;

        // Are we too full?
        var unitsToDrain = Math.Min(bloodstreamVolume.Float(),bloodsuckerComp.UnitsToSucc);

        var stomachAvailableVolume = stomachSolution.Value.Comp.Solution.AvailableVolume;

        if (stomachAvailableVolume < unitsToDrain)
            unitsToDrain = (float) stomachAvailableVolume;

        if (unitsToDrain <= 2)
        {
            _popups.PopupEntity(Loc.GetString("drink-component-try-use-drink-had-enough"), bloodsucker, bloodsucker, Shared.Popups.PopupType.MediumCaution);
            return false;
        }

        _adminLogger.Add(Shared.Database.LogType.MeleeHit, Shared.Database.LogImpact.Medium, $"{ToPrettyString(bloodsucker):player} sucked blood from {ToPrettyString(victim):target}");

        // All good, succ time.
        _audio.PlayPvs("/Audio/Items/drink.ogg", bloodsucker);
        _popups.PopupEntity(Loc.GetString("bloodsucker-blood-sucked-victim", ("sucker", bloodsucker)), victim, victim, Shared.Popups.PopupType.LargeCaution);
        var doNotify = true;

        if (_mindSystem.TryGetMind(bloodsucker, out var bloodsuckermidId, out _))
        {
            EnsureComp<BloodSuckedComponent>(victim).BloodSuckerMindId = bloodsuckermidId;

            if (_roleSystem.MindHasRole<VampireRoleComponent>(bloodsuckermidId, out var role))
            {
                var vpm = role.Value.Comp2;
                vpm.Drink += unitsToDrain;

                if (TryComp<BkmVampireComponent>(bloodsucker, out var bkmVampireComponent))
                {
                    _leveling.AddCurrency((bloodsucker,bkmVampireComponent),

                        (1 * (vpm.Tier + 1)) // 1 * (Тир + 1) * коэффицент

                        * BloodPrice((bloodsucker,bkmVampireComponent), victim, unitsToDrain)

                        , "укус");
                    doNotify = false;
                }
            }
        }
        else
        {
            EnsureComp<BloodSuckedComponent>(victim).BloodSuckerMindId = null;
        }

        if(doNotify)
            _popups.PopupEntity(Loc.GetString("bloodsucker-blood-sucked", ("target", victim)), bloodsucker, bloodsucker, Shared.Popups.PopupType.Medium);


        var bloodSolution = bloodstream.BloodSolution.Value;
        // Make everything actually ingest.
        var temp = _solutionSystem.SplitSolution(bloodSolution, unitsToDrain);
        _reactiveSystem.DoEntityReaction(bloodsucker, temp, ReactionMethod.Ingestion);
        _stomachSystem.TryTransferSolution(stomachList.Value.Owner, temp, stomachList.Value);

        // Add a little pierce
        DamageSpecifier damage = new();
        damage.DamageDict.Add("Piercing", 1); // Slowly accumulate enough to gib after like half an hour

        _damageableSystem.TryChangeDamage(victim, damage, true, true, origin: bloodsucker);

        if (bloodsuckerComp.InjectWhenSucc && _solutionSystem.TryGetSolution(victim, bloodstream.ChemicalSolutionName, out var chemical))
        {
            _solutionSystem.TryAddReagent(chemical.Value, bloodsuckerComp.InjectReagent, bloodsuckerComp.UnitsToInject, out var acceptedQuantity);
        }


        return true;
    }

    private float BloodPrice(Entity<BkmVampireComponent> vamp, EntityUid victim, float unitsToDrain)
    {
        var pr = 1f;
        if (HasComp<BloodSuckerComponent>(victim))
        {
            pr -= 0.6F;
        }

        if (!TryComp<DnaComponent>(victim, out var dnaComponent))
        {
            pr -= 0.6F;
        }
        else
        {
            vamp.Comp.DNA.TryAdd(dnaComponent.DNA, 0);

            var blood = vamp.Comp.DNA[dnaComponent.DNA];
            vamp.Comp.DNA[dnaComponent.DNA] += unitsToDrain;

            var factor = (float)Math.Pow(1 - 0.03, blood.Double());
            pr -= 0.6F * (1 - factor);
        }

        pr *= unitsToDrain / 20;

        return Math.Max(0F,pr);
    }
}
