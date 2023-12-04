using Content.Server.Backmen.Psionics;
using Content.Shared.Actions;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Stunnable;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Abilities.Psionics;

public sealed class PsionicInvisibilityPowerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedStunSystem _stunSystem = default!;
    [Dependency] private readonly SharedPsionicAbilitiesSystem _psionics = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PsionicInvisibilityPowerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<PsionicInvisibilityPowerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PsionicInvisibilityPowerComponent, PsionicInvisibilityPowerActionEvent>(OnPowerUsed);
        SubscribeLocalEvent<PsionicInvisibilityPowerOffActionEvent>(OnPowerOff);
        SubscribeLocalEvent<PsionicInvisibilityUsedComponent, ComponentInit>(OnStart);
        SubscribeLocalEvent<PsionicInvisibilityUsedComponent, ComponentShutdown>(OnEnd);
        SubscribeLocalEvent<PsionicInvisibilityUsedComponent, DamageChangedEvent>(OnDamageChanged);
    }

    [ValidatePrototypeId<EntityPrototype>] private const string ActionPsionicInvisibility = "ActionPsionicInvisibility";
    [ValidatePrototypeId<EntityPrototype>] private const string ActionPsionicInvisibilityOff = "ActionPsionicInvisibilityOff";

    private void OnInit(EntityUid uid, PsionicInvisibilityPowerComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.PsionicInvisibilityPowerAction, ActionPsionicInvisibility);

        if (_actions.TryGetActionData(component.PsionicInvisibilityPowerAction, out var action) && action?.UseDelay != null)
            _actions.SetCooldown(component.PsionicInvisibilityPowerAction, _gameTiming.CurTime,
                _gameTiming.CurTime + (TimeSpan)  action?.UseDelay!);

        if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
            psionic.PsionicAbility = component.PsionicInvisibilityPowerAction;
    }

    private void OnShutdown(EntityUid uid, PsionicInvisibilityPowerComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.PsionicInvisibilityPowerAction);
    }

    private void OnPowerUsed(EntityUid uid, PsionicInvisibilityPowerComponent component, PsionicInvisibilityPowerActionEvent args)
    {
        if (HasComp<PsionicInvisibilityUsedComponent>(uid))
            return;

        ToggleInvisibility(args.Performer);

        _actions.AddAction(uid, ref component.PsionicInvisibilityPowerActionOff, ActionPsionicInvisibilityOff);

        _psionics.LogPowerUsed(uid, "psionic invisibility");
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
        EnsureComp<PsionicallyInvisibleComponent>(uid);
        EnsureComp<PacifiedComponent>(uid);
        var stealth = EnsureComp<StealthComponent>(uid);
        _stealth.SetVisibility(uid, 0.66f, stealth);
        _audio.PlayPvs("/Audio/Effects/toss.ogg", uid);

    }

    private void OnEnd(EntityUid uid, PsionicInvisibilityUsedComponent component, ComponentShutdown args)
    {
        if (Terminating(uid))
            return;

        RemComp<PsionicallyInvisibleComponent>(uid);
        RemComp<PacifiedComponent>(uid);
        RemComp<StealthComponent>(uid);
        _audio.PlayPvs("/Audio/Effects/toss.ogg", uid);

        if (TryComp<PsionicInvisibilityPowerComponent>(uid, out var invisibilityPowerComponent))
        {
            _actions.RemoveAction(uid, invisibilityPowerComponent.PsionicInvisibilityPowerActionOff);
        }

        _stunSystem.TryParalyze(uid, TimeSpan.FromSeconds(8), false);
        DirtyEntity(uid);
    }

    private void OnDamageChanged(EntityUid uid, PsionicInvisibilityUsedComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        ToggleInvisibility(uid);
    }

    public void ToggleInvisibility(EntityUid uid)
    {
        if (!HasComp<PsionicInvisibilityUsedComponent>(uid))
        {
            EnsureComp<PsionicInvisibilityUsedComponent>(uid);
        }
        else
        {
            RemComp<PsionicInvisibilityUsedComponent>(uid);
        }
    }
}
