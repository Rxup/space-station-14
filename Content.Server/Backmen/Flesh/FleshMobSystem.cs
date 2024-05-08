using System.Numerics;
using Content.Server.Popups;
using Content.Shared.Backmen.Flesh;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Flesh;

public sealed class FleshMobSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FleshMobComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<FleshMobComponent, AttackAttemptEvent>(OnAttackAttempt);

    }

    private void OnAttackAttempt(EntityUid uid, FleshMobComponent component, AttackAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (HasComp<FleshMobComponent>(args.Target))
        {
            _popup.PopupCursor(Loc.GetString("flesh-mob-cant-atack-flesh-mob"), uid,
                PopupType.LargeCaution);
            args.Cancel();
        }
        if (HasComp<FleshCultistComponent>(args.Target))
        {
            _popup.PopupCursor(Loc.GetString("flesh-mob-cant-atack-flesh-cultist"), uid,
                PopupType.LargeCaution);
            args.Cancel();
        }

        if (HasComp<FleshHeartComponent>(args.Target))
        {
            _popup.PopupCursor(Loc.GetString("flesh-mob-cant-atack-flesh-heart"), uid,
                PopupType.LargeCaution);
            args.Cancel();
        }
    }

    private const float SpawnDistance = 0.25f;
    private void OnMobStateChanged(EntityUid uid, FleshMobComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (component.SoundDeath != null)
            _audioSystem.PlayPvs(component.SoundDeath, uid, component.SoundDeath.Params);

        if (component.DeathMobSpawnId == null)
            return;

        var pos = Transform(uid);

        for (var i = 0; i < component.DeathMobSpawnCount; i++)
        {
            var coords = new EntityCoordinates(
                pos.Coordinates.EntityId,
                pos.Coordinates.Position with
                {
                    X = pos.Coordinates.Position.X + _random.NextFloat(-SpawnDistance,SpawnDistance),
                    Y = pos.Coordinates.Position.Y + _random.NextFloat(-SpawnDistance,SpawnDistance),
                }
            );
            Spawn(component.DeathMobSpawnId, coords);
        }
    }
}

