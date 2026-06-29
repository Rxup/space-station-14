using Content.Shared.Actions;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Backmen.Abilities.Psionics;

public sealed partial class PsychokinesisPowerSystem : SharedPsychokinesisPowerSystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPsionicAbilitiesSystem _psionics = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    protected override void EnsurePowerActions(EntityUid uid, PsychokinesisPowerComponent component)
    {
        _actions.AddAction(uid, ref component.PsychokinesisPowerAction, ActionPsychokinesis);

        var actionEnt = _actions.GetAction(component.PsychokinesisPowerAction);
        if (actionEnt is { Comp.UseDelay: {} delay })
            _actions.SetCooldown(component.PsychokinesisPowerAction, delay);

        if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
            psionic.PsionicAbility = component.PsychokinesisPowerAction;
    }

    protected override void RemovePowerActions(EntityUid uid, PsychokinesisPowerComponent component)
    {
        _actions.RemoveAction(uid, component.PsychokinesisPowerAction);
    }

    protected override void HandlePowerUse(EntityUid uid, PsychokinesisPowerComponent component, PsychokinesisPowerActionEvent args)
    {
        if(args.Handled)
            return;

        var transform = Transform(args.Performer);

        if (transform.MapID != _transform.GetMapId(args.Target))
            return;

        if(transform.GridUid != _transform.GetGrid(args.Target))
            return;

        if(component.TeleportEffect != null)
            SpawnAtPosition(component.TeleportEffect, transform.Coordinates);
        _transform.SetCoordinates(args.Performer, args.Target);
        _transform.AttachToGridOrMap(args.Performer);

        if(component.TeleportOutEffect != null)
            SpawnAtPosition(component.TeleportOutEffect, args.Target);
        _audio.PlayPvs(component.WaveSound, args.Performer, AudioParams.Default.WithVolume(component.WaveVolume));

        _psionics.LogPowerUsed(uid, "psychokinesis");

        args.Handled = true;
    }

    private readonly EntProtoId ActionPsychokinesis = "ActionPsychokinesis";
}

