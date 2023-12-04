using Content.Shared.Actions;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

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

    [ValidatePrototypeId<EntityPrototype>] private const string ActionPsychokinesis = "ActionPsychokinesis";

    private void OnInit(EntityUid uid, PsychokinesisPowerComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.PsychokinesisPowerAction, ActionPsychokinesis);

        if (_actions.TryGetActionData(component.PsychokinesisPowerAction, out var action) && action?.UseDelay != null)
            _actions.SetCooldown(component.PsychokinesisPowerAction, _gameTiming.CurTime,
                _gameTiming.CurTime + (TimeSpan)  action?.UseDelay!);

        if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
            psionic.PsionicAbility = component.PsychokinesisPowerAction;
    }

    private void OnShutdown(EntityUid uid, PsychokinesisPowerComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.PsychokinesisPowerAction);
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

