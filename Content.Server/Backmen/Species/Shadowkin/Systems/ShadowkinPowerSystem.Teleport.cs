using Content.Server.Backmen.Species.Shadowkin.Components;
using Content.Server.Backmen.Species.Shadowkin.Events;
using Content.Server.Magic;
using Content.Server.Backmen.Species.Shadowkin.Components;
using Content.Server.Backmen.Species.Shadowkin.Events;
using Content.Shared.Actions;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Species.Shadowkin.Components;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Backmen.Species.Shadowkin.Components;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Species.Shadowkin.Systems;

public sealed class ShadowkinTeleportSystem : EntitySystem
{
    [Dependency] private readonly ShadowkinPowerSystem _power = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MagicSystem _magic = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShadowkinTeleportPowerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ShadowkinTeleportPowerComponent, ComponentShutdown>(Shutdown);

        SubscribeLocalEvent<ShadowkinTeleportPowerComponent, ShadowkinTeleportEvent>(Teleport);
    }

    [ValidatePrototypeId<EntityPrototype>] private const string ShadowkinTeleport = "ShadowkinTeleport";
    private void OnInit(Entity<ShadowkinTeleportPowerComponent> ent, ref ComponentInit args)
    {
        _actions.AddAction(ent, ref ent.Comp.ShadowkinTeleportAction, ShadowkinTeleport);
    }

    private void Shutdown(EntityUid uid, ShadowkinTeleportPowerComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.ShadowkinTeleportAction);
    }

    private static SoundSpecifier _soundTeleport = new SoundPathSpecifier("/Audio/Backmen/Effects/Shadowkin/Powers/teleport.ogg");
    public bool DoTeleport(EntityUid user, EntityCoordinates target, SoundSpecifier? sound = null, float? soundVolume = 5f)
    {
        if(!_interaction.InRangeUnobstructed(user,
               target,
               0,
               CollisionGroup.Opaque,
               predicate: (ent) => _tagSystem.HasTag(ent, "Structure"),
               popup:true))
            return false;

        var userPos = Transform(user);

        if (userPos.MapID != _transform.GetMapId(target) ||
            userPos.GridUid == null ||
            _transform.GetGrid(target) is not {} grid)
            return false;

        PullableComponent? pullable = null; // To avoid "might not be initialized when accessed" warning
        if (TryComp<PullerComponent>(user, out var puller) &&
            puller.Pulling != null &&
            TryComp(puller.Pulling, out pullable) &&
            pullable.BeingPulled)
        {
            // Temporarily stop pulling to avoid not teleporting to the target
            _pulling.TryStopPull(puller.Pulling.Value, pullable);
        }

        // Teleport the performer to the target
        _transform.SetCoordinates(user, target);
        _transform.AttachToGridOrMap(user);

        if (pullable != null && puller?.Pulling != null)
        {
            // Get transform of the pulled entity
            //var pulledTransform = Transform(puller.Pulling!.Value);

            // Teleport the pulled entity to the target
            // TODO: Relative position to the performer
            _transform.SetCoordinates(puller.Pulling.Value, target);
            _transform.AttachToGridOrMap(puller.Pulling!.Value);

            // Resume pulling
            // TODO: This does nothing? // This does things sometimes, but the client never knows
            _pulling.TryStartPull(user, puller.Pulling.Value, puller, pullable);
        }

        // Play the teleport sound
        _audio.PlayPvs(sound ?? _soundTeleport, user, AudioParams.Default.WithVolume(soundVolume ?? 5f));

        return true;
    }

    private void Teleport(EntityUid uid, ShadowkinTeleportPowerComponent component, ShadowkinTeleportEvent args)
    {
        // Need power to drain power
        if (!HasComp<ShadowkinComponent>(args.Performer))
            return;

        // Don't activate abilities if handcuffed
        // TODO: Something like the Psionic Headcage to disable powers for Shadowkin
        if (HasComp<HandcuffComponent>(args.Performer) || HasComp<PsionicInsulationComponent>(args.Performer))
            return;

        if(!DoTeleport(args.Performer, args.Target, args.Sound, args.Volume))
            return;

        // Take power and deal stamina damage
        _power.TryAddPowerLevel(args.Performer, -args.PowerCost);
        _stamina.TakeStaminaDamage(args.Performer, args.StaminaCost);

        args.Handled = true;
    }
}
