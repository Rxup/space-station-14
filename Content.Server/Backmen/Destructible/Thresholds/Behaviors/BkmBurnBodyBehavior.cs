using Content.Server.Backmen.Body.Systems;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Behaviors;
using JetBrains.Annotations;

namespace Content.Server.Backmen.Destructible.Thresholds.Behaviors;

[UsedImplicitly]
[DataDefinition]
public sealed partial class BkmBurnBodyBehavior : IThresholdBehavior
{
    public void Execute(EntityUid bodyId, DestructibleSystem system, EntityUid? cause = null)
    {
        system.EntityManager.System<BkmBurnBodySystem>().BurnMobToAsh(bodyId, cause);
    }
}
