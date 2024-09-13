using Content.Server.Decals;
using Content.Server.Footprints.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Decals;
using Content.Shared.Inventory;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Server.Footprint.Systems;


public sealed partial class FootprintSystem : EntitySystem
{

    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    const float MaxAlpha = 0.5f;
    const float MinAlpha = 0.2f;

    const float DeltaAlpha = MaxAlpha - MinAlpha;

    const string ShoeSlot = "shoes";

    public override void Update(float frametime)
    {
        var query = EntityQueryEnumerator<CanLeaveFootprintsComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var currentFootprintComp, out var transform))
        {
            if (!CanLeaveFootprints(uid, out var messMaker) ||
                !TryComp<LeavesFootprintsComponent>(messMaker, out var footprintComp))
                continue;

            var posUid = messMaker;
            var angle = transform.LocalRotation;

            if (HasComp<ClothingComponent>(messMaker) &&
                _container.TryGetContainingContainer((messMaker, null, null), out var container) &&
                TryComp<TransformComponent>(container.Owner, out var xform))
            {
                posUid = container.Owner;
                angle = xform.LocalRotation;
            }

            var newPos = _transform.GetMapCoordinates(posUid);
            var oldPos = currentFootprintComp.LastFootstep;

            if (newPos == MapCoordinates.Nullspace)
                continue;

            if (newPos.MapId != oldPos.MapId)
            {
                DoFootprint(messMaker, currentFootprintComp, footprintComp, newPos, angle);
                return;
            }

            var delta = Vector2.Distance(newPos.Position, oldPos.Position);

            if (delta < footprintComp.Distance)
                continue;

            DoFootprint(messMaker, currentFootprintComp, footprintComp, newPos, angle);
        }
    }

    private void DoFootprint(EntityUid uid, CanLeaveFootprintsComponent currentFootprintComp, LeavesFootprintsComponent footprintComp, MapCoordinates pos, Angle angle)
    {
        var decal = footprintComp.FootprintDecal;

        if (currentFootprintComp.UseAlternative != null)
        {
            if (currentFootprintComp.UseAlternative.Value)
                decal = footprintComp.FootprintDecalAlternative;

            currentFootprintComp.UseAlternative ^= true;
        }

        if (!_prototypeManager.TryIndex<DecalPrototype>(decal, out var footprintDecal))
        {
            RemComp<CanLeaveFootprintsComponent>(uid);
            return;
        }

        if (!_mapManager.TryFindGridAt(pos, out var gridUid, out var grid))
            return;

        var color = currentFootprintComp.Color;

        float onePiece = DeltaAlpha / (footprintComp.MaxFootsteps); //IS REAL!!
        color = color.WithAlpha(MinAlpha + (onePiece * currentFootprintComp.FootstepsLeft));

        var coords = new EntityCoordinates(gridUid, _map.WorldToLocal(gridUid, grid, pos.Position));

        _decals.TryAddDecal(footprintDecal.ID, coords, out _, color, angle, cleanable: true);

        currentFootprintComp.FootstepsLeft -= 1;

        if (currentFootprintComp.FootstepsLeft <= 0)
        {
            RemComp<CanLeaveFootprintsComponent>(uid);
            return;
        }

        currentFootprintComp.LastFootstep = pos;
        currentFootprintComp.Color = color;
    }

    private bool CanLeaveFootprints(EntityUid uid, out EntityUid messMaker)
    {
        messMaker = EntityUid.Invalid;

        if (_inventory.TryGetSlotEntity(uid, ShoeSlot, out var shoe) &&
            EntityManager.HasComponent<LeavesFootprintsComponent>(shoe)) // check if their shoes have it too
        {
            messMaker = shoe.Value;
        }
        else if (EntityManager.HasComponent<LeavesFootprintsComponent>(uid))
        {
            if (shoe != null)
                RemComp<CanLeaveFootprintsComponent>(shoe.Value);

            messMaker = uid;
        }
        else
        {
            CleanupFootprintComp(uid, shoe);
            return false;
        }

        if (messMaker == EntityUid.Invalid ||
            !HasComp<LeavesFootprintsComponent>(messMaker))
        {
            CleanupFootprintComp(uid, shoe);
            return false;
        }

        return true;
    }

    private void CleanupFootprintComp(EntityUid player, EntityUid? shoe)
    {
        RemComp<CanLeaveFootprintsComponent>(player);

        if (shoe != null)
            RemComp<CanLeaveFootprintsComponent>(shoe.Value);
    }
}