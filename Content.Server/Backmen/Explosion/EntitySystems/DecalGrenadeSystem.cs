using System.Numerics;
using Content.Server.Decals;
using Content.Server.Backmen.Explosion.Components;
using Content.Server.Explosion.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Explosion.EntitySystems
{
    public sealed class DecalGrenadeSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly TransformSystem _transformSystem = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly DecalSystem _decalSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<DecalGrenadeComponent, TriggerEvent>(OnFragTrigger);
        }

        /// <summary>
        /// Triggered when the grenade explodes, spawns decals around the explosion.
        /// </summary>
        private void OnFragTrigger(Entity<DecalGrenadeComponent> entity, ref TriggerEvent args)
        {
            var component = entity.Comp;

            if (component.DecalPrototypes == null || component.DecalPrototypes.Count == 0)
                return;

            SpawnDecals(entity.Owner, component);
            args.Handled = true;
        }

        /// <summary>
        /// Spawns decals in radius around the grenade explosion.
        /// </summary>
        private void SpawnDecals(EntityUid grenadeUid, DecalGrenadeComponent component)
        {
            if (!TryComp<TransformComponent>(grenadeUid, out var grenadeXform))
                return;

            var grenadePosition = grenadeXform.Coordinates;

            for (int i = 0; i < component.DecalCount; i++)
            {
                float radius = component.DecalRadius * _random.NextFloat();

                float angle = _random.NextFloat() * MathF.Tau;

                Vector2 offset = new Vector2(
                    radius * MathF.Cos(angle),
                    radius * MathF.Sin(angle));

                EntityCoordinates decalPosition = grenadePosition.Offset(offset);

                var decalPrototype = component.GetRandomDecal();

                if (decalPrototype != null)
                {
                    _decalSystem.TryAddDecal(
                        decalPrototype,
                        decalPosition,
                        out _,
                        cleanable: true);
                }
            }
        }
    }
}
