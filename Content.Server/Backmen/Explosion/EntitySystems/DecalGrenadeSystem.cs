using System.Numerics;
using Content.Server.Decals;
using Content.Server.Backmen.Explosion.Components;
using Content.Shared.Trigger;
using Robust.Server.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Map;

namespace Content.Server.Backmen.Explosion.EntitySystems
{
    public sealed partial class DecalGrenadeSystem : EntitySystem
    {
        [Dependency] private IRobustRandom _random = default!;
        [Dependency] private TransformSystem _transformSystem = default!;
        [Dependency] private IEntityManager _entityManager = default!;
        [Dependency] private DecalSystem _decalSystem = default!;

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
            if (!TryComp(grenadeUid, out TransformComponent? grenadeXform))
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

                string? decalPrototype = component.GetRandomDecal();

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
