using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Server.Polymorph.Systems;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Boss;

[Serializable]
[DataDefinition]
public sealed partial class PolymorphBehavior : IThresholdBehavior
{
    [DataField(required: true)]
    public List<ProtoId<PolymorphPrototype>> Prototypes = [];

    [DataField]
    public float Chance = 1.0f;

    public void Execute(EntityUid owner, DestructibleSystem system, EntityUid? cause = null)
    {
        if (!system.Random.Prob(Chance))
            return;

        var proto = system.Random.Pick(Prototypes);

        var pSys = system.EntityManager.System<PolymorphSystem>();
        pSys.PolymorphEntity(owner, proto);
    }
}
