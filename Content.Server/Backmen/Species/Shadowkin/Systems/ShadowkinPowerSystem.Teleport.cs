using Content.Server.Backmen.Species.Shadowkin.Components;
using Content.Server.Backmen.Species.Shadowkin.Events;
using Content.Shared.Actions;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Species.Shadowkin.Components;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Species.Shadowkin.Systems;

public sealed partial class ShadowkinTeleportSystem : EntitySystem
{
    [Dependency] private ShadowkinPowerSystem _power = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private Shared.StatusEffectNew.StatusEffectsSystem _statusEffects = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly EntProtoId ShadowkinTeleport = "ShadowkinTeleport";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShadowkinTeleportPowerComponent, MapInitEvent>(OnMapInit, after: [typeof(ActionGrantSystem)]);
        SubscribeLocalEvent<ShadowkinTeleportPowerComponent, ComponentShutdown>(Shutdown);

        SubscribeLocalEvent<ShadowkinTeleportPowerComponent, ShadowkinTeleportEvent>(Teleport);
    }

    private void OnMapInit(Entity<ShadowkinTeleportPowerComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.ShadowkinTeleportAction is { Valid: true })
            return;

        if (TryComp<ActionGrantComponent>(ent, out var grant))
        {
            foreach (var action in grant.ActionEntities)
            {
                if (MetaData(action).EntityPrototype?.ID == ShadowkinTeleport)
                {
                    ent.Comp.ShadowkinTeleportAction = action;
                    return;
                }
            }
        }

        _actions.AddAction(ent, ref ent.Comp.ShadowkinTeleportAction, ShadowkinTeleport);
    }

    private void Shutdown(EntityUid uid, ShadowkinTeleportPowerComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.ShadowkinTeleportAction);
    }

    private static readonly SoundSpecifier SoundTeleport = new SoundPathSpecifier("/Audio/Backmen/Effects/Shadowkin/Powers/teleport.ogg");

    /// <param name="allowThroughGlass">
    /// Manual teleport may pass through glass (Opaque-only LoS). Forced/auto teleports must not —
    /// going through a window into space is a common death.
    /// </param>
    public bool DoTeleport(
        EntityUid user,
        EntityCoordinates target,
        SoundSpecifier? sound = null,
        float? soundVolume = 5f,
        bool allowThroughGlass = true,
        bool popup = true)
    {
        // Glass uses GlassLayer (Impassable, no Opaque). Opaque-only lets you go *through* glass;
        // adding Impassable blocks the path for auto-teleport.
        var collisionMask = allowThroughGlass
            ? CollisionGroup.Opaque
            : CollisionGroup.Opaque | CollisionGroup.Impassable;

        if (!_interaction.InRangeUnobstructed(user, target, 0, collisionMask, popup: popup))
            return false;

        var userPos = Transform(user);

        if (userPos.MapID != _transform.GetMapId(target) ||
            userPos.GridUid == null ||
            _transform.GetGrid(target) is not { })
            return false;

        // Through glass is fine; landing *in* glass/walls is not.
        if (!_turf.TryGetTileRef(target, out var tileRef) ||
            _turf.IsTileBlocked(tileRef.Value, CollisionGroup.Impassable))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("shadowkin-teleport-blocked"), user, user);
            return false;
        }

        // Auto-teleport must not dump the shadowkin into space either.
        if (!allowThroughGlass && _turf.IsSpace(tileRef.Value))
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
            // Teleport the pulled entity to the target
            // TODO: Relative position to the performer
            _transform.SetCoordinates(puller.Pulling.Value, target);
            _transform.AttachToGridOrMap(puller.Pulling!.Value);

            // Resume pulling
            // TODO: This does nothing? // This does things sometimes, but the client never knows
            _pulling.TryStartPull(user, puller.Pulling.Value, puller, pullable);
        }

        // Play the teleport sound
        _audio.PlayPvs(sound ?? SoundTeleport, user, AudioParams.Default.WithVolume(soundVolume ?? 5f));

        return true;
    }

    private void Teleport(EntityUid uid, ShadowkinTeleportPowerComponent component, ShadowkinTeleportEvent args)
    {
        // Need power to drain power
        if (!HasComp<ShadowkinComponent>(args.Performer))
            return;

        // Don't activate abilities if handcuffed
        // TODO: Something like the Psionic Headcage to disable powers for Shadowkin
        if (HasComp<HandcuffComponent>(args.Performer))
            return;

        if (_statusEffects.HasEffectComp<PsionicInsulationComponent>(args.Performer))
            return;

        if (!DoTeleport(args.Performer, args.Target, args.Sound, args.Volume))
            return;

        // Take power and deal stamina damage
        _power.TryAddPowerLevel(args.Performer, -args.PowerCost);
        _stamina.TakeStaminaDamage(args.Performer, args.StaminaCost);

        args.Handled = true;
    }
}
