using Content.Server.Station.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Backmen.Disease;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Random.Helpers;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Disease.StationEvents;

public sealed class DiseaseOutbreakRule : StationEventSystem<DiseaseOutbreakRuleComponent>
{
    [Dependency] private readonly DiseaseSystem _diseaseSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;

    /// <summary>
    /// Finds 2-5 random, alive entities that can host diseases
    /// and gives them a randomly selected disease.
    /// They all get the same disease.
    /// </summary>
    protected override void Started(EntityUid uid, DiseaseOutbreakRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        HashSet<EntityUid> stationsToNotify = new();
        List<Entity<DiseaseCarrierComponent>> aliveList = new();



        var q = EntityQueryEnumerator<DiseaseCarrierComponent, MobStateComponent>();
        while (q.MoveNext(out var owner, out var carrier, out var mobState))
        {
            var station = StationSystem.GetOwningStation(owner);
            if (!HasComp<StationEventEligibleComponent>(station))
                continue;

            if (_mobStateSystem.IsDead(owner, mobState) || _mobStateSystem.IsCritical(owner, mobState))
                continue;

            aliveList.Add((owner,carrier));
        }
        RobustRandom.Shuffle(aliveList);

        // We're going to filter the above out to only alive mobs. Might change after future mobstate rework
        var toInfect = RobustRandom.Next(2, 5);

        var diseaseName = RobustRandom.Pick(component.NotTooSeriousDiseases);

        var disease = PrototypeManager.Index(diseaseName);

        // Now we give it to people in the list of living disease carriers earlier
        foreach (var target in aliveList)
        {
            if (toInfect-- == 0)
                break;

            _diseaseSystem.TryAddDisease(target.Owner, disease, target);

            var station = StationSystem.GetOwningStation(target.Owner);
            if(station == null)
                continue;
            stationsToNotify.Add((EntityUid) station);
        }
    }
}
