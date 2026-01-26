using Content.Shared.Actions;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics.Events;
using Content.Shared.Backmen.Species.Shadowkin.Components;
using Content.Shared.Eye;
using Content.Shared.StatusEffectNew;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Abilities.Psionics;

public sealed class MetapsionicPowerSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popups = default!;
    [Dependency] private readonly SharedPsionicAbilitiesSystem _psionics = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;

    private EntityQuery<PsionicInsulationComponent>  _insulationQuery;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MetapsionicPowerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MetapsionicPowerComponent, MetapsionicPowerActionEvent>(OnPowerUsed);
        SubscribeLocalEvent<MetapsionicVisibleComponent, ComponentStartup>(OnAddCanSeeAll);
        SubscribeLocalEvent<MetapsionicVisibleComponent, ComponentShutdown>(OnRemoveCanSeeAll);
        SubscribeLocalEvent<MetapsionicVisibleComponent, GetVisMaskEvent>(OnGetVisMask);

        _insulationQuery = GetEntityQuery<PsionicInsulationComponent>();
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

    private void OnInit(EntityUid uid, MetapsionicPowerComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.MetapsionicPowerAction, ActionMetapsionicPulse);

        var actionEnt = _actions.GetAction(component.MetapsionicPowerAction);
        if (actionEnt is { Comp.UseDelay: {} delay })
            _actions.SetCooldown(component.MetapsionicPowerAction, delay);

        if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
            psionic.PsionicAbility = component.MetapsionicPowerAction;
    }

    private static readonly EntProtoId MetapsionicVisibleStatus = "MetapsionicVisible";

    private void OnPowerUsed(EntityUid uid, MetapsionicPowerComponent component, MetapsionicPowerActionEvent args)
    {
        if(args.Handled)
            return;

        _statusEffects.TryAddStatusEffectDuration(uid, MetapsionicVisibleStatus, TimeSpan.FromSeconds(10));
        var coord = Transform(uid).Coordinates;

        foreach (var entity in _lookup.GetEntitiesInRange<PsionicComponent>(coord, component.Range))
        {
            if (entity.Owner != uid && !_insulationQuery.HasComp(entity)
                                    //&& !(HasComp<ClothingGrantPsionicPowerComponent>(entity) && Transform(entity).ParentUid == uid)
                                    )
            {
                _popups.PopupEntity(Loc.GetString("metapsionic-pulse-success"), uid, uid, PopupType.LargeCaution);
                args.Handled = true;
                return;
            }
        }

        foreach (var entity in _lookup.GetEntitiesInRange<ShadowkinDarkSwappedComponent>(coord, component.Range))
        {
            if (entity.Owner != uid && !_insulationQuery.HasComp(entity)
                //&& !(HasComp<ClothingGrantPsionicPowerComponent>(entity) && Transform(entity).ParentUid == uid)
               )
            {
                _popups.PopupEntity(Loc.GetString("metapsionic-pulse-shadowkin-success"), uid, uid, PopupType.LargeCaution);
                args.Handled = true;
                return;
            }
        }
        _popups.PopupEntity(Loc.GetString("metapsionic-pulse-failure"), uid, uid, PopupType.Large);
        _psionics.LogPowerUsed(uid, "metapsionic pulse", 2, 4);

        args.Handled = true;
    }
}
