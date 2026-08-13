using Content.Shared.Backmen.Explosion.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Shared.Backmen.Explosion;

public sealed partial class RMCExplosionShockWaveSystem : EntitySystem
{
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;

    public static readonly EntProtoId ShockWaveKey = "StatusEffectShockWave";

    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(0.5);

    private readonly HashSet<EntityUid> _inRange = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RMCExplosionShockWaveComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<RMCExplosionShockWaveComponent> ent, ref ComponentStartup args)
    {
        if (_timing.ApplyingState)
            return;

        var duration = DefaultDuration;
        if (TryComp<TimedDespawnComponent>(ent, out var despawn))
            duration = TimeSpan.FromSeconds(despawn.Lifetime);

        _inRange.Clear();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, ent.Comp.Range, _inRange);

        foreach (var uid in _inRange)
        {
            if (!HasComp<ActorComponent>(uid))
                continue;

            _statusEffects.TryAddStatusEffectDuration(uid, ShockWaveKey, duration);
        }
    }
}
