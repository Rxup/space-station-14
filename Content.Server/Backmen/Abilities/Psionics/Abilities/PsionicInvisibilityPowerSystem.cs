using Content.Shared.Actions;
using Content.Shared.CombatMode.Pacification;
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
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Abilities.Psionics;

public sealed partial class PsionicInvisibilityPowerSystem : StatusEffectGrantedPowerSystem<PsionicInvisibilityPowerComponent, PsionicInvisibilityPowerActionEvent>
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedStunSystem _stunSystem = default!;
    [Dependency] private SharedPsionicAbilitiesSystem _psionics = default!;
    [Dependency] private SharedStealthSystem _stealth = default!;

    private static readonly EntProtoId StatusEffectPsionicInvisibility = "StatusEffectPsionicInvisibility";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PsionicInvisibilityPowerOffActionEvent>(OnPowerOff);
        SubscribeLocalEvent<CancelPsionicInvisibilityAlertEvent>(OnAlertCancel);

        // Status-effect path (component lives on the effect entity)
        SubscribeLocalEvent<PsionicInvisibilityUsedComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<PsionicInvisibilityUsedComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<PsionicInvisibilityUsedComponent, StatusEffectRelayedEvent<DamageChangedEvent>>(OnDamageRelayed);
        SubscribeLocalEvent<PsionicInvisibilityUsedComponent, StatusEffectRelayedEvent<WoundsChangedEvent>>(OnWoundRelayed);

        // Fallback: component placed directly on a mob (maps / legacy)
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

        if (StatusEffects.HasStatusEffect(args.Performer, StatusEffectPsionicInvisibility) ||
            HasComp<PsionicInvisibilityUsedComponent>(args.Performer))
            return;

        if (!StatusEffects.TrySetStatusEffectDuration(args.Performer, StatusEffectPsionicInvisibility))
            return;

        _actions.AddAction(args.Performer, ref component.PsionicInvisibilityPowerActionOff, component.ActionPsionicInvisibilityOff);

        _psionics.LogPowerUsed(args.Performer, "psionic invisibility");
        args.Handled = true;
    }

    private void OnPowerOff(PsionicInvisibilityPowerOffActionEvent args)
    {
        if (!TryCancelInvisibility(args.Performer))
            return;

        args.Handled = true;
    }

    private void OnAlertCancel(CancelPsionicInvisibilityAlertEvent args)
    {
        if (args.Handled)
            return;

        if (!TryCancelInvisibility(args.User))
            return;

        args.Handled = true;
    }

    private void OnApplied(Entity<PsionicInvisibilityUsedComponent> ent, ref StatusEffectAppliedEvent args)
    {
        StartInvisibility(args.Target, ent.Comp);
    }

    private void OnRemoved(Entity<PsionicInvisibilityUsedComponent> ent, ref StatusEffectRemovedEvent args)
    {
        EndInvisibility(args.Target, ent.Comp);
    }

    private void OnStart(EntityUid uid, PsionicInvisibilityUsedComponent component, ComponentInit args)
    {
        // Status-effect entity: StatusEffectApplied handles the real target.
        if (HasComp<StatusEffectComponent>(uid))
            return;

        StartInvisibility(uid, component);
    }

    private void OnEnd(EntityUid uid, PsionicInvisibilityUsedComponent component, ComponentShutdown args)
    {
        if (HasComp<StatusEffectComponent>(uid))
            return;

        EndInvisibility(uid, component);
    }

    private void OnDamageRelayed(Entity<PsionicInvisibilityUsedComponent> ent, ref StatusEffectRelayedEvent<DamageChangedEvent> args)
    {
        if (!args.Args.DamageIncreased)
            return;

        if (TryComp<StatusEffectComponent>(ent.Owner, out var status) && status.AppliedTo is { } target)
            TryCancelInvisibility(target);
    }

    private void OnWoundRelayed(Entity<PsionicInvisibilityUsedComponent> ent, ref StatusEffectRelayedEvent<WoundsChangedEvent> args)
    {
        if (!args.Args.DamageIncreased)
            return;

        if (TryComp<StatusEffectComponent>(ent.Owner, out var status) && status.AppliedTo is { } target)
            TryCancelInvisibility(target);
    }

    private void OnDamageChanged(EntityUid uid, PsionicInvisibilityUsedComponent component, DamageChangedEvent args)
    {
        if (HasComp<StatusEffectComponent>(uid) || !args.DamageIncreased)
            return;

        TryCancelInvisibility(uid);
    }

    private void OnWoundDamage(EntityUid uid, PsionicInvisibilityUsedComponent component, WoundsChangedEvent args)
    {
        if (HasComp<StatusEffectComponent>(uid) || !args.DamageIncreased)
            return;

        TryCancelInvisibility(uid);
    }

    /// <summary>
    /// Cancels active power invisibility (status effect and/or legacy direct component).
    /// </summary>
    public bool TryCancelInvisibility(EntityUid uid)
    {
        var removed = false;

        if (StatusEffects.HasStatusEffect(uid, StatusEffectPsionicInvisibility))
            removed |= StatusEffects.TryRemoveStatusEffect(uid, StatusEffectPsionicInvisibility);

        // Legacy / map: component on the mob itself
        if (HasComp<PsionicInvisibilityUsedComponent>(uid) && !HasComp<StatusEffectComponent>(uid))
        {
            RemCompDeferred<PsionicInvisibilityUsedComponent>(uid);
            removed = true;
        }

        return removed;
    }

    public void ToggleInvisibility(EntityUid uid)
    {
        if (StatusEffects.HasStatusEffect(uid, StatusEffectPsionicInvisibility) ||
            HasComp<PsionicInvisibilityUsedComponent>(uid))
        {
            TryCancelInvisibility(uid);
            return;
        }

        StatusEffects.TrySetStatusEffectDuration(uid, StatusEffectPsionicInvisibility);
    }

    private void StartInvisibility(EntityUid uid, PsionicInvisibilityUsedComponent component)
    {
        component.Pacify = HasComp<PacifiedComponent>(uid);
        // PsionicallyInvisible lives on the status-effect entity; layer is applied via StatusEffectApplied → Target.
        EnsureComp<PacifiedComponent>(uid);
        var stealth = EnsureComp<StealthComponent>(uid);
        _stealth.SetVisibility(uid, 0.66f, stealth);
        _audio.PlayPvs("/Audio/Effects/toss.ogg", uid);
    }

    private void EndInvisibility(EntityUid uid, PsionicInvisibilityUsedComponent component)
    {
        if (TerminatingOrDeleted(uid))
            return;

        // Layer removal is handled by PsionicallyInvisible StatusEffectRemoved → Target.

        if (!component.Pacify)
            RemComp<PacifiedComponent>(uid);

        RemCompDeferred<StealthComponent>(uid);
        _audio.PlayPvs("/Audio/Effects/toss.ogg", uid);

        TryGetInvisibilityPowerComponent(uid, out var invisibilityPowerComponent);
        TryRemoveAttachedAction(uid, invisibilityPowerComponent?.PsionicInvisibilityPowerActionOff);
        _stunSystem.TryUpdateParalyzeDuration(uid, TimeSpan.FromSeconds(invisibilityPowerComponent?.StunSecond ?? 8));
        DirtyEntity(uid);
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
