using Content.Server.Roles;
using Content.Shared.Mind;
using Content.Shared.Objectives.Interfaces;

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
