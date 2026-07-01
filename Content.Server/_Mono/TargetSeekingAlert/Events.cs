using Content.Server._Mono.Projectiles.TargetSeeking;

namespace Content.Server._Mono.TargetSeekingAlert;

[ByRefEvent]
public readonly record struct TargetSeekerAlertActivatedEvent;

[ByRefEvent]
public readonly record struct TargetSeekerAlertDeactivatedEvent();

[ByRefEvent]
public readonly record struct TargetSeekerAlertStartedBeingTargetedEvent(Entity<TargetSeekingComponent, TransformComponent> Seeker, bool Active);

[ByRefEvent]
public readonly record struct TargetSeekerAlertStoppedBeingTargetedEvent(Entity<TargetSeekingComponent, TransformComponent> Seeker, bool Active);
