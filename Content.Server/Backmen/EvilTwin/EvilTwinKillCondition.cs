using Content.Server.Objectives.Conditions;
using Content.Shared.Mind;
using Content.Shared.Objectives.Interfaces;
using JetBrains.Annotations;

namespace Content.Server.Backmen.EvilTwin;

[UsedImplicitly]
[DataDefinition]
public sealed partial class EvilTwinKillCondition : KillPersonCondition
{
    public override IObjectiveCondition GetAssigned(EntityUid mindId, MindComponent mind)
    {
        if (!IoCManager.Resolve<IEntityManager>().TryGetComponent<EvilTwinComponent>(mind.OwnedEntity, out var twin))
        {
            return new EscapeShuttleCondition();
        }
        if (twin.TwinMind == null)
        {
            return new DieCondition();
        }
        return new EvilTwinKillCondition
        {
            TargetMindId = twin.TwinEntity
        };
    }
}
