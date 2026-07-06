using Content.Server.Backmen.Body.Systems;
using Content.Shared.Backmen.Supermatter.Components;

namespace Content.Server.Backmen.Supermatter;

public sealed partial class SupermatterSystem
{
    protected override void DustEntity(EntityUid uid, BkmSupermatterComponent supermatter, EntityUid target)
    {
        var burnBody = EntityManager.System<BkmBurnBodySystem>();

        if (!burnBody.TryDustEntity(target, uid))
            burnBody.DustFlatEntity(target, uid);
    }
}
