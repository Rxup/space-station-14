using Content.Server.Backmen.Psionics;
using Content.Shared.Actions;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics;
using Content.Shared.Backmen.Psionics.Components;
using Content.Shared.Backmen.Psionics.Events;
using Content.Shared.Backmen.Species.Shadowkin.Components;
using Content.Shared.Eye;
using Content.Shared.StatusEffect;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Abilities.Psionics;

public sealed class MetapsionicPowerSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popups = default!;
    [Dependency] private readonly SharedPsionicAbilitiesSystem _psionics = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly PsionicInvisibilitySystem _invisibilitySystem = default!;
    [Dependency] private readonly VisibilitySystem _visibility = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MetapsionicPowerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MetapsionicPowerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MetapsionicPowerComponent, MetapsionicPowerActionEvent>(OnPowerUsed);
        SubscribeLocalEvent<MetapsionicVisibleComponent, ComponentStartup>(OnAddCanSeeAll);
        SubscribeLocalEvent<MetapsionicVisibleComponent, ComponentShutdown>(OnRemoveCanSeeAll);
    }

    private void OnRemoveCanSeeAll(Entity<MetapsionicVisibleComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<EyeComponent>(ent, out var eye))
            return;

        if(HasComp<PotentialPsionicComponent>(ent) && !HasComp<PsionicallyInvisibleComponent>(ent))
            _invisibilitySystem.SetCanSeePsionicInvisiblity(ent, false, eye);

        var vm = (VisibilityFlags)eye.VisibilityMask;
        if (vm.HasFlag(VisibilityFlags.DarkSwapInvisibility) && !HasComp<ShadowkinDarkSwappedComponent>(ent))
        {
            _eye.SetVisibilityMask(ent, eye.VisibilityMask & ~(int) VisibilityFlags.DarkSwapInvisibility, eye);

            var eyeEnt = (ent.Owner, EnsureComp<VisibilityComponent>(ent));
            _visibility.RemoveLayer(eyeEnt, (int) VisibilityFlags.DarkSwapInvisibility, false);
            _visibility.RefreshVisibility(eyeEnt);
        }
    }

    private void OnAddCanSeeAll(Entity<MetapsionicVisibleComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<EyeComponent>(ent, out var eye))
            return;
        _invisibilitySystem.SetCanSeePsionicInvisiblity(ent, true);

        var vm = (VisibilityFlags)eye.VisibilityMask;
        if (!vm.HasFlag(VisibilityFlags.DarkSwapInvisibility))
        {
            _eye.SetVisibilityMask(ent, eye.VisibilityMask | (int) VisibilityFlags.DarkSwapInvisibility, eye);

            var eyeEnt = (ent.Owner, EnsureComp<VisibilityComponent>(ent));
            _visibility.AddLayer(eyeEnt, (int) VisibilityFlags.DarkSwapInvisibility, false);
            _visibility.RefreshVisibility(eyeEnt);
        }
    }

    [ValidatePrototypeId<EntityPrototype>] private const string ActionMetapsionicPulse = "ActionMetapsionicPulse";

    private void OnInit(EntityUid uid, MetapsionicPowerComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.MetapsionicPowerAction, ActionMetapsionicPulse);

        if (_actions.TryGetActionData(component.MetapsionicPowerAction, out var action) && action?.UseDelay != null)
            _actions.SetCooldown(component.MetapsionicPowerAction, _gameTiming.CurTime,
                _gameTiming.CurTime + (TimeSpan)  action?.UseDelay!);

        if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
            psionic.PsionicAbility = component.MetapsionicPowerAction;
    }

    private void OnShutdown(EntityUid uid, MetapsionicPowerComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.MetapsionicPowerAction);
    }

    private void OnPowerUsed(EntityUid uid, MetapsionicPowerComponent component, MetapsionicPowerActionEvent args)
    {
        if(args.Handled)
            return;

        _statusEffects.TryAddStatusEffect<MetapsionicVisibleComponent>(uid, "SeeAll", TimeSpan.FromSeconds(2), true);
        var coord = Transform(uid).Coordinates;
        foreach (var entity in _lookup.GetEntitiesInRange<PsionicComponent>(coord, component.Range))
        {
            if (entity.Owner != uid && !HasComp<PsionicInsulationComponent>(entity)
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
            if (entity.Owner != uid && !HasComp<PsionicInsulationComponent>(entity)
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
