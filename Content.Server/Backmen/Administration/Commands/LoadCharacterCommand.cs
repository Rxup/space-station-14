using System.Linq;
using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Players;
using Content.Shared.Preferences;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class LoadCharacterCommand : IConsoleCommand
{
        [Dependency] private readonly IEntitySystemManager _entitySys = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IServerPreferencesManager _prefs = default!;

        public string Command => "loadcharacter";
        public string Description => Loc.GetString("loadcharacter-command-description");
        public string Help => Loc.GetString("loadcharacter-command-help");

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var player = shell.Player;
            if (player == null)
            {
                shell.WriteError(Loc.GetString("shell-only-players-can-run-this-command"));
                return;
            }

            var mind = player.ContentData();

            if (mind == null)
            {
                shell.WriteError(Loc.GetString("shell-entity-is-not-mob")); // No mind specific errors? :(
                return;
            }

            EntityUid target;

            if (args.Length >= 1)
            {

                if (!int.TryParse(args.First(), out var entInt))
                {
                    shell.WriteLine(Loc.GetString("shell-entity-uid-must-be-number"));
                    return;
                }

                var targetNet = new NetEntity(entInt);

                if (!_entityManager.TryGetEntity(targetNet, out var uid))
                {
                    shell.WriteLine(Loc.GetString("shell-invalid-entity-id"));
                    return;
                }

                target = uid.Value;
            }
            else
            {
                if (player.AttachedEntity == null ||
                    !_entityManager.HasComponent<HumanoidAppearanceComponent>(player.AttachedEntity.Value))
                {
                    shell.WriteError(Loc.GetString("shell-must-be-attached-to-entity"));
                    return;
                }
                target = player.AttachedEntity.Value;
            }

            if (!target.IsValid() || !_entityManager.EntityExists(target))
            {
                shell.WriteLine(Loc.GetString("shell-invalid-entity-id"));
                return;
            }

            if (!_entityManager.TryGetComponent<HumanoidAppearanceComponent>(target, out var humanoidAppearance))
            {
                shell.WriteError(Loc.GetString("shell-entity-with-uid-lacks-component", ("uid", target.ToString()), ("componentName", nameof(HumanoidAppearanceComponent))));
                return;
            }

            HumanoidCharacterProfile character;

            if (args.Length >= 2)
            {
                // This seems like a bad way to go about it, but it works so eh?
                var name = String.Join(" ", args.Skip(1).ToArray());
                shell.WriteLine(Loc.GetString("loadcharacter-command-fetching", ("name", name)));

                var charIndex = _prefs.GetPreferences(mind.UserId).Characters.FirstOrNull(p => p.Value.Name == name)?.Key ?? -1;
                if (charIndex < 0)
                {
                    shell.WriteError(Loc.GetString("loadcharacter-command-fetching-failed"));
                    return;
                }

                character = (HumanoidCharacterProfile) _prefs.GetPreferences(mind.UserId).GetProfile(charIndex);
            }
            else
                character = (HumanoidCharacterProfile) _prefs.GetPreferences(mind.UserId).SelectedCharacter;

            // This shouldn't ever fail considering the previous checks
            if (!_prototypeManager.TryIndex<SpeciesPrototype>(humanoidAppearance.Species, out var speciesPrototype) || !_prototypeManager.TryIndex<SpeciesPrototype>(character.Species, out var entPrototype))
                return;

            if (speciesPrototype != entPrototype)
                shell.WriteLine(Loc.GetString("loadcharacter-command-mismatch"));

            var coordinates = player.AttachedEntity != null
                ? _entityManager.GetComponent<TransformComponent>(player.AttachedEntity.Value).Coordinates
                : _entitySys.GetEntitySystem<GameTicker>().GetObserverSpawnPoint();

            _entityManager.System<StationSpawningSystem>()
                .SpawnPlayerMob(coordinates: coordinates, profile: character, entity: target, job: null, station: null);

            shell.WriteLine(Loc.GetString("loadcharacter-command-complete"));
        }

        public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
        {
            if (args.Length == 1)
            {
                return CompletionResult.FromHint(Loc.GetString("shell-argument-uid"));
            }
            if (args.Length == 2)
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
