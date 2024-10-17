using Content.Server.Administration.Logs;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Database;
using Content.Shared.Implants.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio.Systems;
using Content.Shared.Chemistry;
using Content.Shared.IdentityManagement;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Popups;

namespace Content.Server.Backmen.Implants
{

    public sealed partial class InjectImplantSystem : EntitySystem
    {
        [Dependency] private readonly IAdminLogManager _adminLogger = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
        [Dependency] private readonly IEntityManager _entMan = default!;
        [Dependency] private readonly ReactiveSystem _reactiveSystem = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<InjectOnTriggerComponent, TriggerEvent>(OnInjectOnTrigger);
        }

        public bool InjectSolution(EntityUid user, Entity<InjectOnTriggerComponent> implantEnt, string solutionName, float transferAmount)
        {
            var (implant, injectComp) = implantEnt;

            // Try get initial solution
            if (!_solutionContainer.TryGetSolution(implant, solutionName, out var initialSoln, out var initialSolution))
            {
                Log.Error($"Couldnt find solution named {solutionName} in entity {user}");
                return false;
            }

            // Try get insert solution
            if (!_solutionContainer.TryGetInjectableSolution(user, out var targetSoln, out var targetSolution))
            {
                _popup.PopupEntity(Loc.GetString("inject-trigger-cant-inject-message", ("target", Identity.Entity(user, _entMan))), user, user);
                return false;
            }

            var realtransferAmount = FixedPoint2.Min(initialSolution.Volume, targetSolution.AvailableVolume, transferAmount);
            if (realtransferAmount <= 0)
            {
                _popup.PopupEntity(Loc.GetString("inject-trigger-empty-capsule-message"), user, user);
                return false;
            }

            // Move units from init solution to target solution
            var removedSolution = _solutionContainer.SplitSolution(initialSoln.Value, realtransferAmount);
            if (!targetSolution.CanAddSolution(removedSolution))
            {
                _popup.PopupEntity(Loc.GetString("inject-trigger-cant-inject-message", ("target", Identity.Entity(user, _entMan))), user, user);
                return false;
            }

            _audio.PlayPvs(injectComp.InjectSound, user);

            _reactiveSystem.DoEntityReaction(user, removedSolution, ReactionMethod.Injection);
            _solutionContainer.TryAddSolution(targetSoln.Value, removedSolution);

            _popup.PopupEntity(Loc.GetString("inject-trigger-feel-prick-message"), user, user);
            _adminLogger.Add(LogType.ForceFeed, $"{_entMan.ToPrettyString(user):user} used inject implant with a solution {SolutionContainerSystem.ToPrettyString(removedSolution):removedSolution}");

            return true;
        }

        private void OnInjectOnTrigger(EntityUid uid, InjectOnTriggerComponent component, TriggerEvent args)
        {
            // Get user uid
            if (!TryComp<SubdermalImplantComponent>(uid, out var implantComp) ||
                implantComp.ImplantedEntity == null)
                return;
            var user = implantComp.ImplantedEntity.Value;


            // Geting inject solutions form many avaible solutions
            if (component.InjectSolutions.Count == 0)
            {
                _popup.PopupEntity(Loc.GetString("inject-trigger-empty-message"), user, user);
                return;
            }

            foreach (var solData in component.InjectSolutions)
            {
                if (solData.UsedCount >= solData.Charges)
                    continue;

                InjectSolution(user, (uid, component), solData.Name, solData.TransferAmount);
                solData.UsedCount += 1;
                break;
            }

            args.Handled = true;
        }

    }
}