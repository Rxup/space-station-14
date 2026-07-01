using System.Numerics;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.Map;
using Robust.Shared.Spawners;

namespace Content.Server._Mono.Radar;

public sealed partial class HitscanRadarSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HitscanRadarSignatureComponent, HitscanRaycastFiredEvent>(OnHitscanRaycastFired);
    }

    private void OnHitscanRaycastFired(Entity<HitscanRadarSignatureComponent> ent, ref HitscanRaycastFiredEvent ev)
    {
        var data = ev.Data;
        var shooter = data.Shooter ?? data.Gun;
        var shooterCoords = new EntityCoordinates(shooter, Vector2.Zero);
        var radarEntity = Spawn(null, shooterCoords);
        var radarComponent = EnsureComp<HitscanRadarComponent>(radarEntity);

        var startPos = _transform.GetMapCoordinates(shooter).Position;
        var distance = TryComp<HitscanBasicRaycastComponent>(ent, out var raycast)
            ? raycast.MaxDistance
            : 100f;

        if (data.HitEntity != null)
        {
            distance = Vector2.Distance(startPos, _transform.GetMapCoordinates(data.HitEntity.Value).Position);
        }

        var endPos = startPos + data.ShotDirection.Normalized() * distance;
        radarComponent.RadarColor = ent.Comp.RadarColor;
        radarComponent.LineThickness = ent.Comp.LineThickness;
        radarComponent.Enabled = ent.Comp.Enabled;
        radarComponent.LifeTime = ent.Comp.LifeTime;
        InheritShooterSettings(shooter, radarComponent);

        radarComponent.StartPosition = startPos;
        radarComponent.EndPosition = endPos;
        radarComponent.OriginGrid = Transform(shooter).GridUid;

        ScheduleEntityDespawn(radarEntity, radarComponent.LifeTime);
    }
    private void InheritShooterSettings(EntityUid shooter, HitscanRadarComponent radarComponent)
    {
        if (!TryComp<HitscanRadarComponent>(shooter, out var shooterComponent))
            return;

        radarComponent.RadarColor = shooterComponent.RadarColor;
        radarComponent.LineThickness = shooterComponent.LineThickness;
        radarComponent.Enabled = shooterComponent.Enabled;
        radarComponent.LifeTime = shooterComponent.LifeTime;
    }
    private void ScheduleEntityDespawn(EntityUid entity, float lifetime)
    {
        var despawnComponent = EnsureComp<TimedDespawnComponent>(entity);
        despawnComponent.Lifetime = lifetime;
    }
}
