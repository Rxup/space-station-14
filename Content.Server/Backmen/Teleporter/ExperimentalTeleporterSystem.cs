using System.Linq;
using System.Numerics;
using Content.Server._White.Teleporter;
using Content.Server.Body.Systems;
using Content.Server.Disposal.Unit;
using Content.Shared.Backmen.Standing;
using Content.Shared.Charges.Systems;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Ghost;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Server.Audio;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Teleporter;

public sealed partial class ExperimentalTeleporterSystem : EntitySystem
{
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private BodySystem _bodySystem = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private ContainerSystem _containerSystem = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedLayingDownSystem _layingDown = default!;
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExperimentalTeleporterComponent, UseInHandEvent>(OnUse);
    }

    private void OnUse(EntityUid uid, ExperimentalTeleporterComponent component, UseInHandEvent args)
    {
        if (HasComp<BeingDisposedComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("experimental-teleporter-in-disposal"), args.User, args.User);
            return;
        }

        if (_charges.IsEmpty(uid)
            || !TryComp<TransformComponent>(args.User, out var xform)
            || (_containerSystem.IsEntityInContainer(args.User)
                && !_containerSystem.TryRemoveFromContainer(args.User)))
            return;

        var oldCoords = xform.Coordinates;
        var range = _random.Next(component.MinTeleportRange, component.MaxTeleportRange);
        var offset = xform.LocalRotation.ToWorldVec().Normalized();
        var direction = xform.LocalRotation.GetDir().ToVec();
        var newOffset = offset + direction * range;

        var coords = xform.Coordinates.Offset(newOffset).SnapToGrid(EntityManager);

        Teleport(args.User, uid, component, coords, oldCoords);

        if (!TryCheckWall(coords)
            || EmergencyTeleportation(args.User, uid, component, xform, oldCoords, newOffset)
            || HasComp<GhostComponent>(args.User))
            return;

        _bodySystem.GibBody(args.User, true, splatModifier: 3F);
    }

    private bool EmergencyTeleportation(EntityUid uid, EntityUid teleporterUid, ExperimentalTeleporterComponent component, TransformComponent xform, EntityCoordinates oldCoords, Vector2 offset)
    {
        var newOffset = offset + VectorRandomDirection(component, offset, component.EmergencyLength);
        var coords = xform.Coordinates.Offset(newOffset).SnapToGrid(EntityManager);

        if (_charges.IsEmpty(teleporterUid))
            return false;

        Teleport(uid, teleporterUid, component, coords, oldCoords);

        return !TryCheckWall(coords);
    }

    private void Teleport(EntityUid uid, EntityUid teleporterUid, ExperimentalTeleporterComponent component, EntityCoordinates coords, EntityCoordinates oldCoords)
    {
        PlaySoundAndEffects(component, coords, oldCoords);

        _layingDown.LieDownInRange(uid, coords);
        _transform.SetCoordinates(uid, coords);

        _charges.TryUseCharge(teleporterUid);
    }

    private void PlaySoundAndEffects(ExperimentalTeleporterComponent component, EntityCoordinates coords, EntityCoordinates oldCoords)
    {
        _audio.PlayPvs(component.TeleportSound, coords);
        _audio.PlayPvs(component.TeleportSound, oldCoords);

        _entManager.SpawnEntity(component.TeleportInEffect, coords);
        _entManager.SpawnEntity(component.TeleportOutEffect, oldCoords);
    }

    private bool TryCheckWall(EntityCoordinates coords)
    {
        if (!_turf.TryGetTileRef(coords, out var tile)
            || !TryComp<MapGridComponent>(tile.Value.GridUid, out var mapGridComponent))
            return false;

        var anchoredEntities = _mapSystem.GetAnchoredEntities(tile.Value.GridUid, mapGridComponent, coords);

        return anchoredEntities.Any(x => _tag.HasTag(x, "Wall"));
    }

    private Vector2 VectorRandomDirection(ExperimentalTeleporterComponent component, Vector2 offset, int length)
    {
        if (component.RandomRotations.Count == 0)
            return Vector2.Zero;

        var randomRotation = _random.Next(0, component.RandomRotations.Count);
        return Angle.FromDegrees(component.RandomRotations[randomRotation]).RotateVec(offset.Normalized() * length);
    }
}
