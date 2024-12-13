using Content.Shared.Actions;
using Content.Shared.Actions.Events;
using Content.Shared.Administration.Logs;
using Content.Shared.Backmen.Psionics;
using Content.Shared.Backmen.Psionics.Glimmer;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Abilities.Psionics;

public abstract class SharedPsionicAbilitiesSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popups = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly GlimmerSystem _glimmerSystem = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;

    private EntityQuery<PsionicallyInvisibleComponent> _psionicallyInvisibleQuery;
    private EntityQuery<PsionicInsulationComponent> _psionicInsulationQuery;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PsionicsDisabledComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<PsionicsDisabledComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PsionicComponent, PsionicPowerUsedEvent>(OnPowerUsed);
        SubscribeLocalEvent<PsionicComponent, MobStateChangedEvent>(OnMobStateChanged);

        SubscribeLocalEvent<PsiActionComponent, ValidateActionEntityTargetEvent>(OnTryPowerEntityTarget);
        SubscribeLocalEvent<PsiActionComponent, ValidateActionWorldTargetEvent>(OnTryPowerWorldTarget);
        SubscribeLocalEvent<PsiActionComponent, ActionAttemptEvent>(OnTryUsePower);

        _psionicallyInvisibleQuery = GetEntityQuery<PsionicallyInvisibleComponent>();
        _psionicInsulationQuery = GetEntityQuery<PsionicInsulationComponent>();
    }

    private void OnTryUsePower(Entity<PsiActionComponent> ent, ref ActionAttemptEvent args)
    {
        if (_psionicallyInvisibleQuery.HasComp(args.User))
        {
            _popups.PopupCursor(Loc.GetString("cant-use-in-invisible"), PopupType.SmallCaution);
            args.Cancelled = true;
            return;
        }

        if (_psionicInsulationQuery.HasComp(args.User))
        {
            _popups.PopupCursor(Loc.GetString("cant-use-in-insulation"), PopupType.SmallCaution);
            args.Cancelled = true;
            return;
        }

    }

    private void OnTryPowerWorldTarget(Entity<PsiActionComponent> ent, ref ValidateActionWorldTargetEvent args)
    {
        if (!CanUsePsionicAbilities(args.User, args.Target))
            args.Cancelled = true;
    }

    private void OnTryPowerEntityTarget(Entity<PsiActionComponent> ent, ref ValidateActionEntityTargetEvent args)
    {
        if (!CanUsePsionicAbilities(args.User, args.Target))
            args.Cancelled = true;
    }

    public bool CanUsePsionicAbilities(EntityUid performer, EntityUid target, bool popup = true)
    {
        if (_psionicallyInvisibleQuery.HasComp(performer))
        {
            if(popup)
                _popups.PopupCursor(Loc.GetString("cant-use-in-invisible"), performer, PopupType.SmallCaution);
            return false;
        }

        if (_psionicInsulationQuery.HasComp(target) || _psionicInsulationQuery.HasComp(performer))
        {
            if(popup)
                _popups.PopupCursor(Loc.GetString("cant-use-in-insulation"), performer, PopupType.SmallCaution);
            return false;
        }


        if(!_interaction.InRangeUnobstructed(performer, target, 0, CollisionGroup.WallLayer, popup:true))
            return false;


        return true;
    }
    public bool CanUsePsionicAbilities(EntityUid performer, EntityCoordinates target, bool popup = true)
    {
        if (_psionicallyInvisibleQuery.HasComp(performer))
        {
            if(popup)
                _popups.PopupCursor(Loc.GetString("cant-use-in-invisible"),performer);
            return false;
        }

        if (_psionicInsulationQuery.HasComp(performer))
            return false;

        if(!_interaction.InRangeUnobstructed(performer, target, 0,
               CollisionGroup.TeleportLayer,
               predicate: (ent) => _tagSystem.HasTag(ent, "Structure"),
               popup:true))
            return false;

        return true;
    }

    private void OnPowerUsed(EntityUid uid, PsionicComponent component, PsionicPowerUsedEvent args)
    {

        foreach (var entity in _lookup.GetEntitiesInRange<MetapsionicPowerComponent>(Transform(uid).Coordinates, 10f))
        {
            if (entity.Owner == uid || _psionicInsulationQuery.TryComp(entity, out var insul) && !insul.Passthrough)
                continue;

            _popups.PopupEntity(Loc.GetString("metapsionic-pulse-power", ("power", args.Power)), entity, entity, PopupType.LargeCaution);
            args.Handled = true;
            return;
        }
    }

    private void OnInit(EntityUid uid, PsionicsDisabledComponent component, ComponentInit args)
    {
        SetPsionicsThroughEligibility(uid);
    }

    private void OnShutdown(EntityUid uid, PsionicsDisabledComponent component, ComponentShutdown args)
    {
        SetPsionicsThroughEligibility(uid);
    }

    private void OnMobStateChanged(EntityUid uid, PsionicComponent component, MobStateChangedEvent args)
    {
        SetPsionicsThroughEligibility(uid);
    }

    /// <summary>
    /// Checks whether the entity is eligible to use its psionic ability. This should be run after anything that could effect psionic eligibility.
    /// </summary>
    public void SetPsionicsThroughEligibility(EntityUid uid)
    {
        PsionicComponent? component = null;
        if (!Resolve(uid, ref component, false))
            return;

        if (component.PsionicAbility == null)
            return;

        _actions.SetEnabled(component.PsionicAbility, IsEligibleForPsionics(uid));
    }

    private bool IsEligibleForPsionics(EntityUid uid)
    {
        return !_psionicInsulationQuery.HasComp(uid)
               && (!TryComp<MobStateComponent>(uid, out var mobstate) || mobstate.CurrentState == MobState.Alive);
    }

    public void LogPowerUsed(EntityUid uid, string power, int minGlimmer = 8, int maxGlimmer = 12)
    {
        _adminLogger.Add(Database.LogType.Psionics, Database.LogImpact.Medium, $"{ToPrettyString(uid):player} used {power}");
        var ev = new PsionicPowerUsedEvent(uid, power);
        RaiseLocalEvent(uid, ev, false);

        _glimmerSystem.Glimmer += _robustRandom.Next(minGlimmer, maxGlimmer);
    }
}

public sealed class PsionicPowerUsedEvent : HandledEntityEventArgs
{
    public EntityUid User { get; }
    public string Power = string.Empty;

    public PsionicPowerUsedEvent(EntityUid user, string power)
    {
        User = user;
        Power = power;
    }
}

[Serializable]
[NetSerializable]
public sealed class PsionicsChangedEvent : EntityEventArgs
{
    public readonly NetEntity Euid;
    public PsionicsChangedEvent(NetEntity euid)
    {
        Euid = euid;
    }
}
