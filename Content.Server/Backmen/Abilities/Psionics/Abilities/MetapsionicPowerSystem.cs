using Content.Shared.Actions;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics.Events;
using Content.Shared.Backmen.Species.Shadowkin.Components;
using Content.Shared.Eye;
using Content.Shared.StatusEffectNew;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Abilities.Psionics;

public sealed partial class MetapsionicPowerSystem : StatusEffectGrantedPowerSystem<MetapsionicPowerComponent, MetapsionicPowerActionEvent>
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPopupSystem _popups = default!;
    [Dependency] private SharedPsionicAbilitiesSystem _psionics = default!;
    [Dependency] private SharedEyeSystem _eye = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MetapsionicVisibleComponent, ComponentStartup>(OnAddCanSeeAll);
        SubscribeLocalEvent<MetapsionicVisibleComponent, ComponentShutdown>(OnRemoveCanSeeAll);

        SubscribeLocalEvent<MetapsionicVisibleComponent, GetVisMaskEvent>(OnGetVisMask);
        SubscribeLocalEvent<MetapsionicVisibleComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<MetapsionicVisibleComponent, StatusEffectRemovedEvent>(OnRemoved);
    }

    private void OnRemoved(Entity<MetapsionicVisibleComponent> ent, ref StatusEffectRemovedEvent args)
    {
        RemComp<MetapsionicVisibleComponent>(args.Target);
        _eye.RefreshVisibilityMask(args.Target);
    }

    private void OnApplied(Entity<MetapsionicVisibleComponent> ent, ref StatusEffectAppliedEvent args)
    {
        EnsureComp<MetapsionicVisibleComponent>(args.Target);
        _eye.RefreshVisibilityMask(args.Target);
    }

    private void OnGetVisMask(Entity<MetapsionicVisibleComponent> ent, ref GetVisMaskEvent args)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        args.VisibilityMask |= (int)VisibilityFlags.DarkSwapInvisibility;
        args.VisibilityMask |= (int)VisibilityFlags.PsionicInvisibility;
    }

    private void OnRemoveCanSeeAll(Entity<MetapsionicVisibleComponent> ent, ref ComponentShutdown args)
    {
        // Visibility mask will be recalculated automatically by GetVisMaskEvent handler
        _eye.RefreshVisibilityMask(ent.Owner);
    }

    private void OnAddCanSeeAll(Entity<MetapsionicVisibleComponent> ent, ref ComponentStartup args)
    {
        // Visibility mask will be recalculated automatically by GetVisMaskEvent handler
        _eye.RefreshVisibilityMask(ent.Owner);
    }

    private static readonly EntProtoId ActionMetapsionicPulse = "ActionMetapsionicPulse";

    protected override void EnsurePowerActions(EntityUid uid, MetapsionicPowerComponent component)
    {
        _actions.AddAction(uid, ref component.MetapsionicPowerAction, ActionMetapsionicPulse);

        var actionEnt = _actions.GetAction(component.MetapsionicPowerAction);
        if (actionEnt is { Comp.UseDelay: {} delay })
            _actions.SetCooldown(component.MetapsionicPowerAction, delay);

        if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
            psionic.PsionicAbility = component.MetapsionicPowerAction;
    }

    protected override void RemovePowerActions(EntityUid uid, MetapsionicPowerComponent component)
    {
        _actions.RemoveAction(uid, component.MetapsionicPowerAction);
    }

    private static readonly EntProtoId MetapsionicVisibleStatus = "StatusEffectPsionicAllSee";

    protected override void HandlePowerUse(EntityUid uid, MetapsionicPowerComponent component, MetapsionicPowerActionEvent args)
    {
        if (args.Handled)
            return;

        StatusEffects.TryAddStatusEffectDuration(args.Performer, MetapsionicVisibleStatus, TimeSpan.FromSeconds(10));
        var coord = Transform(args.Performer).Coordinates;

        foreach (var entity in _lookup.GetEntitiesInRange<PsionicComponent>(coord, component.Range))
        {
            if (entity.Owner != args.Performer && !StatusEffects.HasEffectComp<PsionicInsulationComponent>(entity)
                                    //&& !(HasComp<ClothingGrantPsionicPowerComponent>(entity) && Transform(entity).ParentUid == uid)
                                    )
            {
                _popups.PopupEntity(Loc.GetString("metapsionic-pulse-success"), args.Performer, args.Performer, PopupType.LargeCaution);
                args.Handled = true;
                return;
            }
        }

        foreach (var entity in _lookup.GetEntitiesInRange<ShadowkinDarkSwappedComponent>(coord, component.Range))
        {
            if (entity.Owner != args.Performer && !StatusEffects.HasEffectComp<PsionicInsulationComponent>(entity)
                //&& !(HasComp<ClothingGrantPsionicPowerComponent>(entity) && Transform(entity).ParentUid == uid)
               )
            {
                _popups.PopupEntity(Loc.GetString("metapsionic-pulse-shadowkin-success"), args.Performer, args.Performer, PopupType.LargeCaution);
                args.Handled = true;
                return;
            }
        }
        _popups.PopupEntity(Loc.GetString("metapsionic-pulse-failure"), args.Performer, args.Performer, PopupType.Large);
        _psionics.LogPowerUsed(args.Performer, "metapsionic pulse", 2, 4);

        args.Handled = true;
    }
}
