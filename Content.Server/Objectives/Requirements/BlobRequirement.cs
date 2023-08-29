using Content.Server.Mind;
using Content.Server.Objectives.Interfaces;
using Content.Server.Roles;

namespace Content.Server.Objectives.Requirements;

[DataDefinition]
public sealed partial class BlobRequirement : IObjectiveRequirement
{
    public bool CanBeAssigned(EntityUid mindId, MindComponent mind)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();
        var roleSystem = entityManager.System<RoleSystem>();
        return roleSystem.MindHasRole<BlobRoleComponent>(mindId);
    }
}
