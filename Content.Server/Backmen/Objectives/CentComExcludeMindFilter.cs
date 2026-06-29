using Content.Shared.Mind;
using Content.Shared.Mind.Filters;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Objectives;

/// <summary>
/// Excludes CentCom department jobs from random kill targets.
/// </summary>
public sealed partial class CentComExcludeMindFilter : MindFilter
{
    private static readonly ProtoId<DepartmentPrototype> CentComDep = "CentCom";

    protected override bool ShouldRemove(Entity<MindComponent> mind, EntityUid? exclude, IEntityManager entMan)
    {
        var roles = entMan.System<SharedRoleSystem>();
        var proto = IoCManager.Resolve<IPrototypeManager>();

        if (!roles.MindHasRole<JobRoleComponent>(mind.Owner, out var job) || job.Value.Comp1.JobPrototype == null)
            return false;

        var centcom = proto.Index(CentComDep);
        return centcom.Roles.Contains(job.Value.Comp1.JobPrototype.Value);
    }
}
