using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.Network;

namespace Content.Shared.EntityEffects.Effects.EntitySpawning;

/// <summary>
/// Spawns a number of entities of a given prototype at the coordinates of this entity.
/// Amount is modified by scale.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class SpawnEntityEntityEffectSystem : EntityEffectSystem<TransformComponent, SpawnEntity>
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<SpawnEntity> args)
    {
        var quantity = args.Effect.Number * (int)Math.Floor(args.Scale);
        var proto = args.Effect.Entity;

        // Chemical reactions target solution entities, which may be hidden inside a container entity.
        // Spawning next to those would insert the new entity into the internal solutions container.
        var spawnTarget = entity.Owner;
        var spawnXform = entity.Comp;
        if (TryComp<SolutionComponent>(entity, out var solution))
        {
            spawnTarget = _solutionContainer.GetSolutionOwner((entity, solution));
            spawnXform = Transform(spawnTarget);
        }

        if (args.Effect.Predicted)
        {
            for (var i = 0; i < quantity; i++)
            {
                PredictedSpawnNextToOrDrop(proto, spawnTarget, spawnXform);
            }
        }
        else if (_net.IsServer)
        {
            for (var i = 0; i < quantity; i++)
            {
                SpawnNextToOrDrop(proto, spawnTarget, spawnXform);
            }
        }
    }
}

/// <inheritdoc cref="BaseSpawnEntityEntityEffect{T}"/>
public sealed partial class SpawnEntity : BaseSpawnEntityEntityEffect<SpawnEntity>;
