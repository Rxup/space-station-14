using Content.Server.Objectives.Conditions;
using Content.Server.Objectives.Interfaces;
using JetBrains.Annotations;

namespace Content.Server.Backmen.EvilTwin;

[UsedImplicitly]
[DataDefinition]
public sealed partial class EvilTwinKillCondition : KillPersonCondition
{
    public override IObjectiveCondition GetAssigned(Content.Server.Mind.Mind mind)
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
            Target = twin.TwinMind
        };
    }
}
