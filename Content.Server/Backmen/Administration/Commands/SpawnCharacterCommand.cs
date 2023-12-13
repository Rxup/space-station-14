using System.Linq;
using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Content.Shared.Players;
using Content.Shared.Preferences;
using Robust.Shared.Console;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class SpawnCharacterCommand : IConsoleCommand
{
        [Dependency] private readonly IEntitySystemManager _entitySys = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IServerPreferencesManager _prefs = default!;

        public string Command => "spawncharacter";
        public string Description => Loc.GetString("spawncharacter-command-description");
        public string Help => Loc.GetString("spawncharacter-command-help");

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var player = shell.Player;
            if (player == null)
            {
                shell.WriteError(Loc.GetString("shell-only-players-can-run-this-command"));
                return;
            }

            var mindSystem = _entitySys.GetEntitySystem<MindSystem>();

            var mind = player.ContentData()?.Mind;

            if (mind == null || !mindSystem.TryGetSession(mind, out var mindData))
            {
                shell.WriteError(Loc.GetString("shell-entity-is-not-mob"));
                return;
            }


            HumanoidCharacterProfile character;

            if (args.Length >= 1)
            {
                // This seems like a bad way to go about it, but it works so eh?
                var name = string.Join(" ", args.ToArray());
                shell.WriteLine(Loc.GetString("loadcharacter-command-fetching", ("name", name)));

                var charIndex = _prefs.GetPreferences(mindData.UserId).Characters.FirstOrNull(p => p.Value.Name == name)?.Key ?? -1;
                if (charIndex < 0)
                {
                    shell.WriteError(Loc.GetString("loadcharacter-command-fetching-failed"));
                    return;
                }

                character = (HumanoidCharacterProfile) _prefs.GetPreferences(mindData.UserId).GetProfile(charIndex);
            }
            else
                character = (HumanoidCharacterProfile) _prefs.GetPreferences(mindData.UserId).SelectedCharacter;


            var coordinates = player.AttachedEntity != null
                ? _entityManager.GetComponent<TransformComponent>(player.AttachedEntity.Value).Coordinates
                : _entitySys.GetEntitySystem<GameTicker>().GetObserverSpawnPoint();

            mindSystem.TransferTo(mind.Value, _entityManager.System<StationSpawningSystem>()
                .SpawnPlayerMob(coordinates: coordinates, profile: character, entity: null, job: null, station: null));

            shell.WriteLine(Loc.GetString("spawncharacter-command-complete"));
        }

        public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
        {
            if (args.Length == 1)
            {
                var player = shell.Player;
                if (player == null)
                    return CompletionResult.Empty;
                var mind = player.ContentData();
                if (mind == null)
                    return CompletionResult.Empty;

                return CompletionResult.FromHintOptions(_prefs.GetPreferences(mind.UserId).Characters.Select(x=>x.Value.Name), Loc.GetString("loadcharacter-command-hint-select"));
            }

            return CompletionResult.Empty;
        }
}
