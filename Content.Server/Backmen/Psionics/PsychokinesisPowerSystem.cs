using Content.Shared.Actions;
using Content.Shared.Actions.ActionTypes;
using Content.Shared.Backmen.Abilities.Psionics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Audio;

namespace Content.Server.Backmen.Abilities.Psionics;

public sealed class PsychokinesisPowerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPsionicAbilitiesSystem _psionics = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PsychokinesisPowerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<PsychokinesisPowerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PsychokinesisPowerComponent, PsychokinesisPowerActionEvent>(OnPowerUsed);
    }

    private void OnInit(EntityUid uid, PsychokinesisPowerComponent component, ComponentInit args)
    {
        if (!_prototypeManager.TryIndex<WorldTargetActionPrototype>("Psychokinesis", out var psychokinesis))
            return;

        component.PsychokinesisPowerAction = new WorldTargetAction(psychokinesis);
        if (psychokinesis.UseDelay != null)
            component.PsychokinesisPowerAction.Cooldown = (_gameTiming.CurTime, _gameTiming.CurTime + (TimeSpan) psychokinesis.UseDelay);
        _actions.AddAction(uid, component.PsychokinesisPowerAction, null);

        if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
            psionic.PsionicAbility = component.PsychokinesisPowerAction;
    }

    private void OnShutdown(EntityUid uid, PsychokinesisPowerComponent component, ComponentShutdown args)
    {
        if (_prototypeManager.TryIndex<EntityTargetActionPrototype>("Psychokinesis", out var psychokinesis))
            _actions.RemoveAction(uid, new EntityTargetAction(psychokinesis), null);
    }

    private void OnPowerUsed(EntityUid uid ,PsychokinesisPowerComponent comp, PsychokinesisPowerActionEvent args)
    {
        var transform = Transform(args.Performer);

        if (transform.MapID != args.Target.GetMapId(EntityManager)) return;

        _transformSystem.SetCoordinates(args.Performer, args.Target);
        transform.AttachToGridOrMap();
        _audio.PlayPvs(comp.WaveSound, args.Performer, AudioParams.Default.WithVolume(comp.WaveVolume));

        _psionics.LogPowerUsed(uid, "psychokinesis");

        args.Handled = true;
    }
}

public sealed partial class PsychokinesisPowerActionEvent : WorldTargetActionEvent {}
