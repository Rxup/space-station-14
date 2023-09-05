using System.Linq;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Objectives.Interfaces;
using Robust.Shared.Random;

namespace Content.Server.Objectives.Conditions;

[DataDefinition]
public sealed partial class KillRandomHeadCondition : KillPersonCondition
{
    // TODO refactor all of this to be ecs
    public override IObjectiveCondition GetAssigned(EntityUid mindId, MindComponent mind)
    {
        RequireDead = true;
        var _stationSystem = EntityManager.System<StationSystem>();

        var allHumans = EntityManager.EntityQuery<MindContainerComponent>(true).Where(mc =>
        {
            var entity = EntityManagerExt.GetComponentOrNull<MindComponent>(EntityManager, (EntityUid?) mc.Mind)?.OwnedEntity;

            if (entity == default)
                return false;

            var station = _stationSystem.GetOwningStation(entity!.Value);
            if (station == null)
            {
                return false;
            }
            if (!EntityManager.HasComponent<StationEventEligibleComponent>(station))
            {
                return false;
            }

            return EntityManager.TryGetComponent(entity, out MobStateComponent? mobState) &&
                   MobStateSystem.IsAlive(entity.Value, mobState) &&
                   mc.Mind != mindId;
        }).Select(mc => mc.Mind).ToList();

        if (allHumans.Count == 0)
            return new DieCondition(); // I guess I'll die

        var allHeads = allHumans
            .Where(mind => Jobs.MindTryGetJob(mind, out _, out var prototype) && prototype.RequireAdminNotify)
            .ToList();

        if (allHeads.Count == 0)
            allHeads = allHumans; // fallback to non-head target

        return new KillRandomHeadCondition { TargetMindId = IoCManager.Resolve<IRobustRandom>().Pick(allHeads) };
    }

    public string Description => Loc.GetString("objective-condition-kill-head-description");
}
