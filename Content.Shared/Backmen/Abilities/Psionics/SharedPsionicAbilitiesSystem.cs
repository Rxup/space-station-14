using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Actions.Events;
using Content.Shared.Administration.Logs;
using Content.Shared.Backmen.Abilities.Psionics.Events;
using Content.Shared.Backmen.Psionics;
using Content.Shared.Backmen.Psionics.Glimmer;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Content.Shared.StatusEffectNew;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Abilities.Psionics;

public abstract partial class SharedPsionicAbilitiesSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPopupSystem _popups = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private GlimmerSystem _glimmerSystem = default!;
    [Dependency] private IRobustRandom _robustRandom = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private TagSystem _tagSystem = default!;
    [Dependency] private Shared.StatusEffectNew.StatusEffectsSystem _statusEffects = default!;
    [Dependency] private MobStateSystem _mobStateSystem = default!;

    [Dependency] private EntityQuery<PsionicallyInvisibleComponent> _psionicallyInvisibleQuery = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PsionicInsulationComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<PsionicInsulationComponent, StatusEffectRemovedEvent>(OnRemoved);

        SubscribeLocalEvent<PsionicComponent, PsionicPowerUsedEvent>(OnPowerUsed);
        SubscribeLocalEvent<PsionicComponent, MobStateChangedEvent>(OnMobStateChanged);

        SubscribeLocalEvent<PsiActionComponent, ActionValidateEvent>(OnActionValidate);
        SubscribeLocalEvent<PsiActionComponent, ActionAttemptEvent>(OnTryUsePower);
        SubscribeLocalEvent<PsiActionComponent, PsiActionToggleEvent>(OnToggleEvent);
        SubscribeLocalEvent<PsiActionComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<PsiActionComponent> ent, ref ExaminedEvent args)
    {
        var action = _actions.GetAction(ent.Owner,false);
        if (action == null)
        {
            return;
        }

        if (!action.Value.Comp.Enabled)
        {
            args.PushMarkup(Loc.GetString("psionic-actions-off"));
        }
    }

    private void OnToggleEvent(Entity<PsiActionComponent> ent, ref PsiActionToggleEvent args)
    {
        var act = _actions.GetAction(ent.Owner, false);
        if(act is not {} actEnt)
            return;
        var actEnt2 = actEnt.AsNullable();

        _actions.SetEnabled(actEnt2, args.Toggle);
    }

    private void OnRemoved(Entity<PsionicInsulationComponent> ent, ref StatusEffectRemovedEvent args)
    {
        SetPsionicsThroughEligibility(args.Target);
    }

    private void OnApplied(Entity<PsionicInsulationComponent> ent, ref StatusEffectAppliedEvent args)
    {
        SetPsionicsThroughEligibility(args.Target);
    }

    private void OnTryUsePower(Entity<PsiActionComponent> ent, ref ActionAttemptEvent args)
    {
        if (_psionicallyInvisibleQuery.HasComp(args.User) || _statusEffects.HasEffectComp<PsionicallyInvisibleComponent>(args.User))
        {
            _popups.PopupCursor(Loc.GetString("cant-use-in-invisible"), PopupType.SmallCaution);
            args.Cancelled = true;
            return;
        }

        if (_statusEffects.HasEffectComp<PsionicInsulationComponent>(args.User))
        {
            _popups.PopupCursor(Loc.GetString("cant-use-in-insulation"), PopupType.SmallCaution);
            args.Cancelled = true;
            return;
        }

    }

    private void OnActionValidate(Entity<PsiActionComponent> ent, ref ActionValidateEvent args)
    {
        if (args.Input.EntityTarget is { } target &&
            TryGetEntity(target, out var targetEnt) &&
            !CanUsePsionicAbilities(args.User, targetEnt.Value))
        {
            args.Invalid = true;
            return;
        }

        if (args.Input.EntityCoordinatesTarget is { } netCoord)
        {
            var coord = GetCoordinates(netCoord);
            if (!coord.IsValid(EntityManager))
            {
                args.Invalid = true;
                return;
            }

            if (!CanUsePsionicAbilities(args.User, coord))
            {
                args.Invalid = true;
                return;
            }
        }
    }

    public bool CanUsePsionicAbilities(EntityUid performer, EntityUid target, bool popup = true)
    {
        if (_psionicallyInvisibleQuery.HasComp(performer) || _statusEffects.HasEffectComp<PsionicallyInvisibleComponent>(performer))
        {
            if(popup)
                _popups.PopupCursor(Loc.GetString("cant-use-in-invisible"), performer, PopupType.SmallCaution);
            return false;
        }

        if (
            _statusEffects.HasEffectComp<PsionicInsulationComponent>(target) ||
            _statusEffects.HasEffectComp<PsionicInsulationComponent>(performer)
            )
        {
            if(popup)
                _popups.PopupCursor(Loc.GetString("cant-use-in-insulation"), performer, PopupType.SmallCaution);
            return false;
        }


        if(!_interaction.InRangeUnobstructed(performer, target, 0, CollisionGroup.WallLayer, popup:true))
            return false;


        return true;
    }

    private static readonly ProtoId<TagPrototype> Structure = "Structure";

    public bool CanUsePsionicAbilities(EntityUid performer, EntityCoordinates target, bool popup = true)
    {
        if (_psionicallyInvisibleQuery.HasComp(performer))
        {
            if(popup)
                _popups.PopupCursor(Loc.GetString("cant-use-in-invisible"),performer);
            return false;
        }

        if (_statusEffects.HasEffectComp<PsionicInsulationComponent>(performer))
            return false;

        if(!_interaction.InRangeUnobstructed(performer, target, 0,
               CollisionGroup.Opaque,
               predicate: (ent) => _tagSystem.HasTag(ent, Structure),
               popup:true))
            return false;

        return true;
    }

    private void OnPowerUsed(EntityUid uid, PsionicComponent component, PsionicPowerUsedEvent args)
    {

        foreach (var entity in _lookup.GetEntitiesInRange<MetapsionicPowerComponent>(Transform(uid).Coordinates, 10f))
        {
            if (entity.Owner == uid)
                continue;

            if (_statusEffects.TryEffectsWithComp<PsionicInsulationComponent>(entity, out var effects) &&
                effects.Any(x=>!x.Comp1.Passthrough))
                continue;

            _popups.PopupEntity(Loc.GetString("metapsionic-pulse-power", ("power", args.Power)), entity, entity, PopupType.LargeCaution);
            args.Handled = true;
            return;
        }
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
        var toggle = IsEligibleForPsionics(uid);
        var ev = new PsiActionToggleEvent(uid, toggle);
        foreach (var action in _actions.GetActions(uid))
        {
            RaiseLocalEvent(action, ev);
        }
    }

    private bool IsEligibleForPsionics(EntityUid uid)
    {
        if (_statusEffects.HasEffectComp<PsionicInsulationComponent>(uid))
            return false;

        return !_mobStateSystem.IsIncapacitated(uid);
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
    public string Power;

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
