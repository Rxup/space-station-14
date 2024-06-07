using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Random;

namespace Content.Server.StationEvents.Events;

public sealed class BluespaceGoatRule : StationEventSystem<BluespaceGoatRuleComponent>
{
    protected override void Added(EntityUid uid, BluespaceGoatRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        var str = Loc.GetString("bluespace-artifact-event-announcement",
            ("sighting", Loc.GetString(RobustRandom.Pick(component.PossibleSighting))));
        ChatSystem.DispatchGlobalAnnouncement(str, colorOverride: Color.FromHex("#18abf5"));
    }

    protected override void Started(EntityUid uid, BluespaceGoatRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var amountToSpawn = 1;//Math.Max(1, (int) MathF.Round(GetSeverityModifier() / 1.5f));
        for (var i = 0; i < amountToSpawn; i++)
        {
            if (!TryFindRandomTile(out _, out _, out _, out var coords))
                return;

            Spawn(component.BsgoatSpawnerPrototype, coords);
            Spawn(component.ArtifactFlashPrototype, coords);

            Sawmill.Info($"Spawning bluespace goat at {coords}!");
        }
    }
}
