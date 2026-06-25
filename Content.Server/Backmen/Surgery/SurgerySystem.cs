using Content.Server.Atmos.Rotting;
using Content.Server.Backmen.Body.Systems;
using Content.Server.Chat.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Server.Popups;
using Content.Shared.Damage;
using Content.Shared.Interaction;
using Content.Shared.Medical.Surgery.Conditions;
using Content.Shared.Medical.Surgery.Effects.Step;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Linq;
using Content.Shared.Backmen.Surgery.Conditions;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Backmen.Surgery;
using Content.Shared.Medical.Surgery.Steps;
using Content.Shared.Body.Part;
using Content.Shared.Backmen.Surgery.Effects.Step;
using Content.Shared.Backmen.Surgery.Tools;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Medical.Surgery;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.Backmen.Surgery;

public sealed partial class SurgerySystem : SharedSurgerySystem
{
    [Dependency] private BkmBodySystem _body = default!;
    [Dependency] private BodySystem _organBody = default!;
    [Dependency] private SharedTargetingSystem _targeting = default!;

    private static readonly EntProtoId SurgeryGraftArachneAbdomen = "SurgeryGraftArachneAbdomen";
    private static readonly EntProtoId SurgeryGraftArachneFront = "SurgeryGraftArachneFront";
    private static readonly EntProtoId SurgeryGraftSpiderLegLeft = "SurgeryGraftSpiderLegLeft";
    private static readonly EntProtoId SurgeryGraftSpiderLegRight = "SurgeryGraftSpiderLegRight";
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private WoundSystem _wounds = default!;

    private readonly List<EntProtoId> _surgeries = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryToolComponent, GetVerbsEvent<UtilityVerb>>(OnUtilityVerb);
        SubscribeLocalEvent<SurgeryTargetComponent, SurgeryStepDamageEvent>(OnSurgeryStepDamage);
        // You might be wondering "why aren't we using StepEvent for these two?" reason being that StepEvent fires off regardless of success on the previous functions
        // so this would heal entities even if you had a used or incorrect organ.
        SubscribeLocalEvent<SurgeryDamageChangeEffectComponent, SurgeryStepDamageChangeEvent>(OnSurgeryDamageChange);
        SubscribeLocalEvent<SurgeryStepEmoteEffectComponent, SurgeryStepEvent>(OnStepScreamComplete);
        SubscribeLocalEvent<SurgeryStepSpawnEffectComponent, SurgeryStepEvent>(OnStepSpawnComplete);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        LoadPrototypes();
    }

    protected override void RefreshUI(EntityUid body)
    {
        var surgeries = new Dictionary<NetEntity, List<EntProtoId>>();
        var missingParts = new Dictionary<string, SurgeryMissingPartChoice>();
        var bothHumanLegsMissing = IsBothHumanLegsMissing(body);
        var hasArachneOrgan = _body.BodyHasArachneOrgan(body);

        foreach (var surgery in _surgeries)
        {
            if (GetSingleton(surgery) is not { } surgeryEnt)
                continue;

            var arachneGraft = HasComp<SurgeryOrganGraftAttachComponent>(surgeryEnt);

            foreach (var part in _targeting.GetSurgeryTargets(body))
            {
                if (arachneGraft)
                    continue;

                var ev = new SurgeryValidEvent(body, part);
                RaiseLocalEvent(surgeryEnt, ref ev);

                if (ev.Cancelled)
                    continue;

                surgeries.GetOrNew(GetNetEntity(part)).Add(surgery);
            }

            if (!TryComp<SurgeryPartRemovedConditionComponent>(surgeryEnt, out var removedComp)
                || !_body.BodyExpectsReattachPart(body, removedComp.Part, removedComp.Symmetry)
                || _body.TryGetWoundableTargetByType(body, removedComp.Part, removedComp.Symmetry, out _))
                continue;

            if (hasArachneOrgan
                && SurgeryBodyPartMapping.TryGetCategory(removedComp.Part, removedComp.Symmetry, out var removedCategory)
                && SurgeryBodyPartMapping.IsHumanLegOrFootCategory(removedCategory))
                continue;

            if (!TryComp<SurgeryPartConditionComponent>(surgeryEnt, out var partCond)
                || !_body.TryGetWoundableTargetByType(body, partCond.Part, partCond.Symmetry, out var anchor))
                continue;

            var evMissing = new SurgeryValidEvent(body, anchor);
            RaiseLocalEvent(surgeryEnt, ref evMissing);

            if (evMissing.Cancelled)
                continue;

            var key = $"human:{removedComp.Part}:{removedComp.Symmetry}";
            if (!missingParts.TryGetValue(key, out var choice))
            {
                var partName = SharedTargetingSystem.FormatBodyPartType(removedComp.Part, removedComp.Symmetry);
                var label = Loc.GetString("surgery-ui-part-missing", ("part", partName));
                choice = new SurgeryMissingPartChoice(GetNetEntity(anchor), label, []);
                missingParts[key] = choice;
            }

            choice.Surgeries.Add(surgery);
        }

        AddArachneMissingPartRows(body, missingParts);

        Log.Debug($"Setting UI state with {surgeries}, {body} and {SurgeryUIKey.Key}");
        _ui.SetUiState(body, SurgeryUIKey.Key, new SurgeryBuiState(surgeries, missingParts.Values.ToList()));
        /*
            Reason we do this is because when applying a BUI State, it rolls back the state on the entity temporarily,
            which just so happens to occur right as we're checking for step completion, so we end up with the UI
            not updating at all until you change tools or reopen the window. I love shitcode.
        */
        _ui.ServerSendUiMessage(body, SurgeryUIKey.Key, new SurgeryBuiRefreshMessage());
    }

    private void SetDamage(EntityUid body,
        DamageSpecifier damage,
        float partMultiplier,
        EntityUid user,
        EntityUid part)
    {
        if (!TryComp<BodyPartComponent>(part, out var partComp)
            && !TryComp<OrganComponent>(part, out _))
            return;

        if (TryComp<BodyPartComponent>(part, out partComp))
        {
            // kinda funky but still works
            if (damage.GetTotal() < 0)
            {
                foreach (var (type, amount) in damage.DamageDict.ToList())
                {
                    _wounds.TryHaltAllBleeding(part, force: true);
                    _wounds.TryHealWoundsOnWoundable(part, -amount, type, out _, ignoreMultipliers: true);
                }
            }
            else
            {
                _damageable.ChangeDamage(body,
                    damage,
                    true,
                    origin: user,
                    partMultiplier: partMultiplier,
                    targetPart: _body.GetTargetBodyPart(partComp));
            }

            return;
        }

        if (damage.GetTotal() < 0)
        {
            foreach (var (type, amount) in damage.DamageDict.ToList())
            {
                _wounds.TryHaltAllBleeding(part, force: true);
                _wounds.TryHealWoundsOnWoundable(part, -amount, type, out _, ignoreMultipliers: true);
            }
        }
        else
        {
            var targetPart = _targeting.GetTargetBodyPart(part) ?? TargetBodyPart.Chest;
            _damageable.ChangeDamage(body,
                damage,
                true,
                origin: user,
                partMultiplier: partMultiplier,
                targetPart: targetPart);
        }
    }

    private void AttemptStartSurgery(Entity<SurgeryToolComponent> ent, EntityUid user, EntityUid target)
    {
        if (!IsLyingDown(target, user))
            return;

        if (TryComp<SurgeryTargetComponent>(user, out var userSurgery) && !userSurgery.CanOperate)
        {
            _popup.PopupEntity(Loc.GetString("surgery-error-cannot-operate"), user, user);
            return;
        }

        if (TryComp<SurgeryTargetComponent>(target, out var targetSurgery) && !targetSurgery.CanBeOperatedOn)
        {
            _popup.PopupEntity(Loc.GetString("surgery-error-cannot-be-operated-on"), user, user);
            return;
        }

        if (user == target && !_config.GetCVar(Shared.Backmen.CCVar.CCVars.CanOperateOnSelf))
        {
            _popup.PopupEntity(Loc.GetString("surgery-error-self-surgery"), user, user);
            return;
        }

        _ui.OpenUi(target, SurgeryUIKey.Key, user);
        RefreshUI(target);
    }

    private void OnUtilityVerb(Entity<SurgeryToolComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanInteract
            || !args.CanAccess
            || !HasComp<SurgeryTargetComponent>(args.Target)
            || TryComp<SurgeryTargetComponent>(args.User, out var userSurgery) && !userSurgery.CanOperate
            || TryComp<SurgeryTargetComponent>(args.Target, out var targetSurgery) && !targetSurgery.CanBeOperatedOn)
            return;

        var user = args.User;
        var target = args.Target;

        var verb = new UtilityVerb()
        {
            Act = () => AttemptStartSurgery(ent, user, target),
            Icon = new SpriteSpecifier.Rsi(new("/Textures/Objects/Specific/Medical/Surgery/scalpel.rsi/"), "scalpel"),
            Text = Loc.GetString("surgery-verb-text"),
            Message = Loc.GetString("surgery-verb-message"),
            DoContactInteraction = true
        };

        args.Verbs.Add(verb);
    }

    private void OnSurgeryStepDamage(Entity<SurgeryTargetComponent> ent, ref SurgeryStepDamageEvent args) =>
        SetDamage(args.Body, args.Damage, args.PartMultiplier, args.User, args.Part);

    private void OnSurgeryDamageChange(Entity<SurgeryDamageChangeEffectComponent> ent, ref SurgeryStepDamageChangeEvent args)
    {
        var damageChange = ent.Comp.Damage;
        if (HasComp<ForcedSleepingStatusEffectComponent>(args.Body))
            damageChange = damageChange * ent.Comp.SleepModifier;

        SetDamage(args.Body, damageChange, 0.5f, args.User, args.Part);
    }
    private void OnStepScreamComplete(Entity<SurgeryStepEmoteEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (HasComp<ForcedSleepingStatusEffectComponent>(args.Body))
            return;

        _chat.TryEmoteWithChat(args.Body, ent.Comp.Emote);
    }
    private void OnStepSpawnComplete(Entity<SurgeryStepSpawnEffectComponent> ent, ref SurgeryStepEvent args) =>
        SpawnAtPosition(ent.Comp.Entity, Transform(args.Body).Coordinates);

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<EntityPrototype>())
            return;

        LoadPrototypes();
    }

    private void LoadPrototypes()
    {
        _surgeries.Clear();
        foreach (var entity in _prototypes.EnumeratePrototypes<EntityPrototype>())
            if (entity.HasComponent<SurgeryComponent>())
                _surgeries.Add(new EntProtoId(entity.ID));
    }

    private static bool IsBothHumanLegsMissing(EntityUid body, BodySystem organBody)
    {
        return !organBody.TryGetOrganByCategory(body, "LegLeft", out _)
               && !organBody.TryGetOrganByCategory(body, "LegRight", out _);
    }

    private bool IsBothHumanLegsMissing(EntityUid body) => IsBothHumanLegsMissing(body, _organBody);

    private bool TryGetTorsoAnchor(EntityUid body, out EntityUid anchor) =>
        _body.TryGetWoundableTargetByType(body, BodyPartType.Chest, null, out anchor);

    private bool IsSurgeryValidOnAnchor(EntityUid body, EntityUid surgeryEnt, EntityUid anchor)
    {
        var ev = new SurgeryValidEvent(body, anchor);
        RaiseLocalEvent(surgeryEnt, ref ev);
        return !ev.Cancelled;
    }

    private void TryAddMissingPartSurgery(
        EntityUid body,
        Dictionary<string, SurgeryMissingPartChoice> missingParts,
        string key,
        string label,
        EntProtoId surgeryId)
    {
        if (!TryGetTorsoAnchor(body, out var anchor)
            || GetSingleton(surgeryId) is not { } surgeryEnt
            || !IsSurgeryValidOnAnchor(body, surgeryEnt, anchor))
            return;

        if (!missingParts.TryGetValue(key, out var choice))
        {
            choice = new SurgeryMissingPartChoice(GetNetEntity(anchor), label, []);
            missingParts[key] = choice;
        }

        if (!choice.Surgeries.Contains(surgeryId))
            choice.Surgeries.Add(surgeryId);
    }

    private static bool HasMissingSpiderLegSlot(EntityUid body, BodyPartSymmetry side, BodySystem organBody)
    {
        var slots = side == BodyPartSymmetry.Left
            ? SurgeryBodyPartMapping.SpiderLegLeftSlots
            : SurgeryBodyPartMapping.SpiderLegRightSlots;

        foreach (var slot in slots)
        {
            if (!organBody.TryGetOrganByCategory(body, slot, out _))
                return true;
        }

        return false;
    }

    private void AddArachneMissingPartRows(EntityUid body, Dictionary<string, SurgeryMissingPartChoice> missingParts)
    {
        var bothHumanLegsMissing = IsBothHumanLegsMissing(body);
        var hasAbdomen = _organBody.TryGetOrganByCategory(body, "ArachneAbdomen", out _);
        var hasFront = _organBody.TryGetOrganByCategory(body, "ArachneFront", out _);

        if (bothHumanLegsMissing && !hasFront)
        {
            TryAddMissingPartSurgery(body, missingParts, "arachne:front",
                Loc.GetString("surgery-ui-part-missing-arachne-front"), SurgeryGraftArachneFront);
        }

        if (hasFront && !hasAbdomen)
        {
            TryAddMissingPartSurgery(body, missingParts, "arachne:abdomen",
                Loc.GetString("surgery-ui-part-missing-arachne-abdomen"), SurgeryGraftArachneAbdomen);
        }

        if (hasAbdomen && HasMissingSpiderLegSlot(body, BodyPartSymmetry.Left, _organBody))
        {
            TryAddMissingPartSurgery(body, missingParts, "arachne:spider-leg-left",
                Loc.GetString("surgery-ui-part-missing-spider-leg-left"), SurgeryGraftSpiderLegLeft);
        }

        if (hasFront && HasMissingSpiderLegSlot(body, BodyPartSymmetry.Right, _organBody))
        {
            TryAddMissingPartSurgery(body, missingParts, "arachne:spider-leg-right",
                Loc.GetString("surgery-ui-part-missing-spider-leg-right"), SurgeryGraftSpiderLegRight);
        }
    }
}
