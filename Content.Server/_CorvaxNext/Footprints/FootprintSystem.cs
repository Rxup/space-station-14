using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._CorvaxNext.Footprints;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Content.Shared.Standing;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CorvaxNext.Footprints;

public sealed class FootprintSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedPuddleSystem _puddle = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public static readonly FixedPoint2 MaxFootprintVolumeOnTile = 50;

    public static readonly EntProtoId FootprintPrototypeId = "Footprint";

    public const string FootprintOwnerSolution = "print";

    public const string FootprintSolution = "print";

    public const string PuddleSolution = "puddle";

    public override void Initialize()
    {
        SubscribeLocalEvent<FootprintComponent, FootprintCleanEvent>(OnAbsorbentPuddleInteract);

        SubscribeLocalEvent<FootprintOwnerComponent, MoveEvent>(OnMove);

        SubscribeLocalEvent<PuddleComponent, MapInitEvent>(OnMapInit);
    }

    private void OnAbsorbentPuddleInteract(Entity<FootprintComponent> entity, ref FootprintCleanEvent e)
    {
        if (!_solution.TryGetSolution(entity.Owner, FootprintSolution, out _, out var puddleSolution))
            return;

        var color = puddleSolution.GetColor(_prototype);

        for (var i = 0; i < entity.Comp.Footprints.Count; i++)
            entity.Comp.Footprints[i] = entity.Comp.Footprints[i] with
            {
                Color = color
            };

        Dirty(entity);

        if (!TryGetNetEntity(entity, out var netFootprint))
            return;

        RaiseNetworkEvent(new FootprintChangedEvent(netFootprint.Value));
    }

    private void OnMove(Entity<FootprintOwnerComponent> entity, ref MoveEvent e)
    {
        var oldPosition = _transform.ToMapCoordinates(e.OldPosition).Position;
        var newPosition = _transform.ToMapCoordinates(e.NewPosition).Position;

        entity.Comp.Distance += Vector2.Distance(newPosition, oldPosition);

        var standing = TryComp<StandingStateComponent>(entity, out var standingState) && standingState.Standing;

        var requiredDistance = standing ? entity.Comp.FootDistance : entity.Comp.BodyDistance;

        if (entity.Comp.Distance < requiredDistance)
            return;

        entity.Comp.Distance -= requiredDistance;

        var transform = Transform(entity);

        if (transform.GridUid is null)
            return;

        if (!TryComp<MapGridComponent>(transform.GridUid.Value, out var gridComponent))
            return;

        EntityCoordinates coordinates = new(entity, standing ? entity.Comp.NextFootOffset : 0, 0);

        entity.Comp.NextFootOffset = -entity.Comp.NextFootOffset;

        var tile = _map.CoordinatesToTile(transform.GridUid.Value, gridComponent, coordinates);

        if (TryPuddleInteraction(entity, (transform.GridUid.Value, gridComponent), tile, standing))
            return;

        Angle rotation;

        if (!standing)
        {
            var oldLocalPosition = _map.WorldToLocal(transform.GridUid.Value, gridComponent, oldPosition);
            var newLocalPosition = _map.WorldToLocal(transform.GridUid.Value, gridComponent, newPosition);

            rotation = (newLocalPosition - oldLocalPosition).ToAngle();
        }
        else
            rotation = transform.LocalRotation;

        FootprintInteraction(entity, (transform.GridUid.Value, gridComponent), tile, coordinates, rotation, standing);
    }

    private bool TryPuddleInteraction(Entity<FootprintOwnerComponent> entity, Entity<MapGridComponent> grid, Vector2i tile, bool standing)
    {
        if (!TryGetAnchoredEntity<PuddleComponent>(grid, tile, out var puddle))
            return false;

        if (!_solution.TryGetSolution(puddle.Value.Owner, PuddleSolution, out var puddleSolution, out _))
            return false;

        if (!_solution.EnsureSolutionEntity(entity.Owner, FootprintOwnerSolution, out _, out var solution, FixedPoint2.Max(entity.Comp.MaxFootVolume, entity.Comp.MaxBodyVolume)))
            return false;

        _solution.TryTransferSolution(puddleSolution.Value, solution.Value.Comp.Solution, GetFootprintVolume(entity, solution.Value));

        _solution.TryTransferSolution(solution.Value, puddleSolution.Value.Comp.Solution, (standing ? entity.Comp.MaxFootVolume : entity.Comp.MaxBodyVolume) - solution.Value.Comp.Solution.Volume);

        _solution.UpdateChemicals(puddleSolution.Value, false);

        return true;
    }

    private void FootprintInteraction(Entity<FootprintOwnerComponent> entity, Entity<MapGridComponent> grid, Vector2i tile, EntityCoordinates coordinates, Angle rotation, bool standing)
    {
        if (!_solution.TryGetSolution(entity.Owner, FootprintOwnerSolution, out var solution, out _))
            return;

        var volume = standing ? GetFootprintVolume(entity, solution.Value) : GetBodyprintVolume(entity, solution.Value);

        if (volume < entity.Comp.MinFootprintVolume)
            return;

        if (!TryGetAnchoredEntity<FootprintComponent>(grid, tile, out var footprint))
        {
            var footprintEntity = SpawnAtPosition(FootprintPrototypeId, coordinates);

            footprint = (footprintEntity, Comp<FootprintComponent>(footprintEntity));
        }

        if (!_solution.EnsureSolutionEntity(footprint.Value.Owner, FootprintSolution, out _, out var footprintSolution, MaxFootprintVolumeOnTile))
            return;

        var color = solution.Value.Comp.Solution.GetColor(_prototype).WithAlpha((float)volume / (float)(standing ? entity.Comp.MaxFootprintVolume : entity.Comp.MaxBodyprintVolume) / 2f);

        _solution.TryTransferSolution(footprintSolution.Value, solution.Value.Comp.Solution, volume);

        if (footprintSolution.Value.Comp.Solution.Volume >= MaxFootprintVolumeOnTile)
        {
            var footprintSolutionClone = footprintSolution.Value.Comp.Solution.Clone();

            Del(footprint);

            _puddle.TrySpillAt(coordinates, footprintSolutionClone, out _, false);

            return;
        }

        var gridCoords = _map.LocalToGrid(grid, grid, coordinates);

        var x = gridCoords.X / grid.Comp.TileSize;
        var y = gridCoords.Y / grid.Comp.TileSize;

        var halfTileSize = grid.Comp.TileSize / 2f;

        x -= MathF.Floor(x) + halfTileSize;
        y -= MathF.Floor(y) + halfTileSize;

        footprint.Value.Comp.Footprints.Add(new(new(x, y), rotation, color, standing ? "foot" : "body"));

        Dirty(footprint.Value);

        if (!TryGetNetEntity(footprint, out var netFootprint))
            return;

        RaiseNetworkEvent(new FootprintChangedEvent(netFootprint.Value));
    }

    private void OnMapInit(Entity<PuddleComponent> entity, ref MapInitEvent e)
    {
        if (HasComp<FootprintComponent>(entity))
            return;

        var transform = Transform(entity);

        if (transform.GridUid is null)
            return;

        if (!TryComp<MapGridComponent>(transform.GridUid.Value, out var gridComponent))
            return;

        var tile = _map.CoordinatesToTile(transform.GridUid.Value, gridComponent, transform.Coordinates);

        if (!TryGetAnchoredEntity<FootprintComponent>((transform.GridUid.Value, gridComponent), tile, out var footprint))
            return;

        if (!_solution.TryGetSolution(footprint.Value.Owner, FootprintSolution, out _, out var footprintSolution))
            return;

        footprintSolution = footprintSolution.Clone();

        Del(footprint);

        _puddle.TrySpillAt(transform.Coordinates, footprintSolution, out _, false);
    }

    private static FixedPoint2 GetFootprintVolume(Entity<FootprintOwnerComponent> entity, Entity<SolutionComponent> solution)
    {
        return FixedPoint2.Min(solution.Comp.Solution.Volume, (entity.Comp.MaxFootprintVolume - entity.Comp.MinFootprintVolume) * (solution.Comp.Solution.Volume / entity.Comp.MaxFootVolume) + entity.Comp.MinFootprintVolume);
    }

    private static FixedPoint2 GetBodyprintVolume(Entity<FootprintOwnerComponent> entity, Entity<SolutionComponent> solution)
    {
        return FixedPoint2.Min(solution.Comp.Solution.Volume, (entity.Comp.MaxBodyprintVolume - entity.Comp.MinBodyprintVolume) * (solution.Comp.Solution.Volume / entity.Comp.MaxBodyVolume) + entity.Comp.MinBodyprintVolume);
    }

    private bool TryGetAnchoredEntity<T>(Entity<MapGridComponent> grid, Vector2i pos, [NotNullWhen(true)] out Entity<T>? entity) where T : IComponent
    {
        var anchoredEnumerator = _map.GetAnchoredEntitiesEnumerator(grid, grid, pos);
        var entityQuery = GetEntityQuery<T>();

        while (anchoredEnumerator.MoveNext(out var ent))
            if (entityQuery.TryComp(ent, out var comp))
            {
                entity = (ent.Value, comp);
                return true;
            }

        entity = null;
        return false;
    }
}
