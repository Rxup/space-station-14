using Content.Server.Backmen.Psionics;
using Content.Shared.Actions;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Stunnable;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics;
using Content.Shared.Backmen.Psionics.Events;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Damage.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Abilities.Psionics;

public sealed class PsionicInvisibilityPowerSystem : StatusEffectGrantedPowerSystem<PsionicInvisibilityPowerComponent, PsionicInvisibilityPowerActionEvent>
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedStunSystem _stunSystem = default!;
    [Dependency] private readonly SharedPsionicAbilitiesSystem _psionics = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeStatusEffectGrantedPower();
        SubscribeLocalEvent<PsionicInvisibilityPowerOffActionEvent>(OnPowerOff);
        SubscribeLocalEvent<PsionicInvisibilityUsedComponent, ComponentInit>(OnStart);
        SubscribeLocalEvent<PsionicInvisibilityUsedComponent, ComponentShutdown>(OnEnd);
        SubscribeLocalEvent<PsionicInvisibilityUsedComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<PsionicInvisibilityUsedComponent, WoundsChangedEvent>(OnWoundDamage);
    }

    protected override void EnsurePowerActions(EntityUid uid, PsionicInvisibilityPowerComponent component)
    {
        _actions.AddAction(uid, ref component.PsionicInvisibilityPowerAction, component.ActionPsionicInvisibility);

        var actionEnt = _actions.GetAction(component.PsionicInvisibilityPowerAction);
        if (actionEnt is { Comp.UseDelay: {} delay })
            _actions.SetCooldown(component.PsionicInvisibilityPowerAction, delay);

        if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
            psionic.PsionicAbility = component.PsionicInvisibilityPowerAction;
    }

    protected override void RemovePowerActions(EntityUid uid, PsionicInvisibilityPowerComponent component)
    {
        _actions.RemoveAction(uid, component.PsionicInvisibilityPowerAction);
        _actions.RemoveAction(uid, component.PsionicInvisibilityPowerActionOff);
    }

    protected override void HandlePowerUse(EntityUid uid, PsionicInvisibilityPowerComponent component, PsionicInvisibilityPowerActionEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<PsionicInvisibilityUsedComponent>(args.Performer))
            return;

        ToggleInvisibility(args.Performer);

        _actions.AddAction(args.Performer, ref component.PsionicInvisibilityPowerActionOff, component.ActionPsionicInvisibilityOff);

        _psionics.LogPowerUsed(args.Performer, "psionic invisibility");
        args.Handled = true;
    }

    private void OnPowerOff(PsionicInvisibilityPowerOffActionEvent args)
    {
        if (!HasComp<PsionicInvisibilityUsedComponent>(args.Performer))
            return;

        ToggleInvisibility(args.Performer);
        args.Handled = true;
    }

    private void OnStart(EntityUid uid, PsionicInvisibilityUsedComponent component, ComponentInit args)
    {
        component.Pacify = HasComp<PacifiedComponent>(uid);
        EnsureComp<PsionicallyInvisibleComponent>(uid);
        EnsureComp<PacifiedComponent>(uid);
        var stealth = EnsureComp<StealthComponent>(uid);
        _stealth.SetVisibility(uid, 0.66f, stealth);
        _audio.PlayPvs("/Audio/Effects/toss.ogg", uid);

    }

    private void OnEnd(EntityUid uid, PsionicInvisibilityUsedComponent component, ComponentShutdown args)
    {
        if (TerminatingOrDeleted(uid))
            return;

        RemCompDeferred<PsionicallyInvisibleComponent>(uid);

        if(!component.Pacify)
            RemComp<PacifiedComponent>(uid);

        RemCompDeferred<StealthComponent>(uid);
        _audio.PlayPvs("/Audio/Effects/toss.ogg", uid);

        TryGetInvisibilityPowerComponent(uid, out var invisibilityPowerComponent);
        TryRemoveAttachedAction(uid, invisibilityPowerComponent?.PsionicInvisibilityPowerActionOff);
        _stunSystem.TryUpdateParalyzeDuration(uid, TimeSpan.FromSeconds(invisibilityPowerComponent?.StunSecond ?? 8));
        DirtyEntity(uid);
    }

    private void OnDamageChanged(EntityUid uid, PsionicInvisibilityUsedComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        RemCompDeferred<PsionicInvisibilityUsedComponent>(uid);
    }

    private void OnWoundDamage(EntityUid uid, PsionicInvisibilityUsedComponent component, WoundsChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        RemCompDeferred<PsionicInvisibilityUsedComponent>(uid);
    }

    public void ToggleInvisibility(EntityUid uid)
    {
        if (!HasComp<PsionicInvisibilityUsedComponent>(uid))
        {
            EnsureComp<PsionicInvisibilityUsedComponent>(uid);
        }
        else
        {
            RemCompDeferred<PsionicInvisibilityUsedComponent>(uid);
        }
    }

    private bool TryGetInvisibilityPowerComponent(EntityUid uid, out PsionicInvisibilityPowerComponent? powerComponent)
    {
        powerComponent = null;
        if (TryComp(uid, out PsionicInvisibilityPowerComponent? baseComponent))
        {
            powerComponent = baseComponent;
            return true;
        }

        if (!StatusEffects.TryEffectsWithComp<PsionicInvisibilityPowerComponent>(uid, out var effects))
            return false;

        foreach (var effect in effects)
        {
            powerComponent = effect.Comp1;
            return true;
        }

        return false;
    }

    private void TryRemoveAttachedAction(EntityUid uid, EntityUid? actionUid)
    {
        if (actionUid == null)
            return;

        var action = _actions.GetAction(actionUid);
        if (action?.Comp.AttachedEntity != uid)
            return;

        _actions.RemoveAction(uid, actionUid);
    }
}
