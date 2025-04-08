using Content.Shared.Atmos;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Temperature;
using Content.Server.Power.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Popups;
using Content.Server.Temperature.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Content.Shared.Examine;
using Content.Shared.Backmen.BluespaceMining;
using Content.Shared.Backmen.Chat;

namespace Content.Server.Backmen.BluespaceMining
{
    public sealed class BluespaceMinerSystem : EntitySystem
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly ExplosionSystem _explosionSystem = default!;
        [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
        [Dependency] private readonly MapSystem _mapSystem = default!;
        [Dependency] private readonly TransformSystem _transform = default!;
        [Dependency] private readonly ChatSystem _chat = default!;

        private readonly List<GasMixture> _environments = new();

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<BluespaceMinerComponent, ComponentStartup>(OnStartup);
            SubscribeLocalEvent<BluespaceMinerComponent, OnTemperatureChangeEvent>(OnTemperatureChange);
            SubscribeLocalEvent<BluespaceMinerComponent, ExaminedEvent>(OnExamine);
        }

        private void OnStartup(EntityUid uid, BluespaceMinerComponent component, ComponentStartup args)
        {
            component.NextSpawnTime = (float)(_gameTiming.CurTime.TotalSeconds + component.SpawnInterval);
        }

        private void OnTemperatureChange(EntityUid uid,
            BluespaceMinerComponent component,
            OnTemperatureChangeEvent args)
        {
            if (args.CurrentTemperature > component.MaxTemperature)
            {
                _chat.TrySendInGameICMessage(uid, "bluespace-miner-too-hot", InGameICChatType.Speak, false);
            }
            else if (args.CurrentTemperature < component.MinTemperature)
            {
                _chat.TrySendInGameICMessage(uid, "bluespace-miner-too-cold", InGameICChatType.Speak, false);
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<BluespaceMinerComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var miner, out var transform))
            {
                bool hasPower = !miner.RequiresPower || IsPowered(uid);

                bool temperatureInRange = true;
                if (TryComp<TemperatureComponent>(uid, out var temp))
                {
                    temperatureInRange = temp.CurrentTemperature >= miner.MinTemperature &&
                                         temp.CurrentTemperature <= miner.MaxTemperature;
                }

                bool newIsActive = hasPower && temperatureInRange;

                if (newIsActive != miner.IsActive)
                {
                    miner.IsActive = newIsActive;
                    miner.NeedsResync = true;
                }

                if (miner.NeedsResync && miner.IsActive)
                {
                    miner.NextSpawnTime = (float)(_gameTiming.CurTime.TotalSeconds + miner.SpawnInterval);
                    miner.NeedsResync = false;
                }

                UpdateAppearance(uid);

                if (!miner.IsActive)
                    continue;

                if (_gameTiming.CurTime.TotalSeconds >= miner.NextSpawnTime)
                {
                    miner.NextSpawnTime = (float)(_gameTiming.CurTime.TotalSeconds + miner.SpawnInterval);
                    SpawnEntities(uid, miner, transform);
                }

                AddHeat(uid, miner, frameTime, transform);

                CheckForExplosion(uid, miner, transform);
            }
        }

        private void SpawnEntities(EntityUid uid, BluespaceMinerComponent component, TransformComponent transform)
        {
            for (int i = 0; i < component.SpawnAmount; i++)
            {
                if (component.EntityTable.Count == 0)
                    continue;

                string entityToSpawn = _random.Pick(component.EntityTable);
                var coords = transform.Coordinates.Offset(_random.NextVector2(0.5f));
                _entityManager.SpawnEntity(entityToSpawn, coords);
            }
        }

        private void AddHeat(EntityUid uid,
            BluespaceMinerComponent component,
            float frameTime,
            TransformComponent xform)
        {
            var position = _transform.GetGridTilePositionOrDefault((uid, xform));
            _environments.Clear();

            if (_atmosphereSystem.GetTileMixture(xform.GridUid, xform.MapUid, position, true) is { } tileMix)
                _environments.Add(tileMix);

            if (xform.GridUid != null)
            {
                var enumerator = _atmosphereSystem.GetAdjacentTileMixtures(xform.GridUid.Value, position, false, true);
                while (enumerator.MoveNext(out var mix))
                {
                    _environments.Add(mix);
                }
            }

            if (_environments.Count > 0)
            {
                var heatPerTile = component.HeatPerSecond * frameTime / _environments.Count;
                foreach (var env in _environments)
                {
                    _atmosphereSystem.AddHeat(env, heatPerTile);
                }
            }
        }

        private void CheckForExplosion(EntityUid uid, BluespaceMinerComponent component, TransformComponent transform)
        {
            var xform = Transform(uid);

            if (xform.GridUid == null)
                return;

            if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
                return;

            var tile = grid.GetTileRef(xform.Coordinates);

            if (tile == null)
                return;

            if (_atmosphereSystem.GetTileMixture(xform.GridUid, null, tile.GridIndices, true) is not { } mixture)
                return;

            var temperature = mixture.Temperature;

            if (temperature > component.ExplosionThreshold)
            {
                Explode(uid);
            }
        }

        private void Explode(EntityUid entity)
        {
            _explosionSystem.QueueExplosion(entity, "Minibomb", 1000f, 4f, 20f, canCreateVacuum: true);
            EntityManager.QueueDeleteEntity(entity);
        }

        private bool IsPowered(EntityUid uid)
        {
            if (!TryComp<ApcPowerReceiverComponent>(uid, out var powerReceiver))
                return true;

            return powerReceiver.Powered;
        }

        private void UpdateAppearance(EntityUid uid)
        {
            if (!TryComp<AppearanceComponent>(uid, out var appearance) ||
                !TryComp<BluespaceMinerVisualsComponent>(uid, out var visuals))
                return;

            BluespaceMinerVisualState state = BluespaceMinerVisualState.IsOff;

            if (!IsPowered(uid) || !TryComp<BluespaceMinerComponent>(uid, out var miner) || !miner.IsActive)
            {
                state = BluespaceMinerVisualState.IsOff;
            }
            else if (TryComp<TemperatureComponent>(uid, out var temp))
            {
                if (temp.CurrentTemperature >= miner.MinTemperature && temp.CurrentTemperature <= miner.MaxTemperature)
                {
                    state = BluespaceMinerVisualState.IsRunning;
                }
            }

            _appearance.SetData(uid, BluespaceMinerVisuals.State, state, appearance);
        }

        private void OnExamine(EntityUid uid, BluespaceMinerComponent component, ExaminedEvent args)
        {
            if (!args.IsInDetailsRange)
                return;

            if (TryComp<TemperatureComponent>(uid, out var temp))
            {
                var currentTemperature = temp.CurrentTemperature - 273.15f;
                if (temp.CurrentTemperature > component.MaxTemperature)
                {
                    args.PushMarkup(Loc.GetString("bluespace-miner-examine-too-hot",
                        ("temperature", currentTemperature.ToString("F1"))));
                }
                else if (temp.CurrentTemperature < component.MinTemperature)
                {
                    args.PushMarkup(Loc.GetString("bluespace-miner-examine-too-cold",
                        ("temperature", currentTemperature.ToString("F1"))));
                }
                else
                {
                    args.PushMarkup(Loc.GetString("bluespace-miner-examine-ok",
                        ("min", component.MinTemperature - 273.15f),
                        ("max", component.MaxTemperature - 273.15f)));
                }
            }
        }
    }
}
