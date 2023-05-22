using Content.Server.Chemistry.Components.SolutionManager;
using Content.Server.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Microsoft.CodeAnalysis;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Solution = Content.Shared.Chemistry.Components.Solution;

namespace Content.Server.Backmen.Medical
{
    public sealed class CorpiumSystem : EntitySystem
    {
        [Dependency] private readonly SolutionContainerSystem _solutions = default!;

        [DataField("CorpiumId", customTypeSerializer: typeof(PrototypeIdSerializer<ReagentPrototype>))]
        public string CorpiumId = "Corpium";

        private new void Initialize()
        {
            SubscribeLocalEvent<CorpiumComponent, AfterInteractEvent>(OnAfterInteract);
        }

        public void OnAfterInteract(EntityUid uid, CorpiumComponent component, AfterInteractEvent args)
        {
            if (!args.CanReach)
                return;

            var solution = new Solution();
            var quality = FixedPoint2.Min(1);
            solution.AddReagent(CorpiumId, quality);
            _solutions.TryGetInjectableSolution(uid, out var injectableSolution);
            if (injectableSolution != null)
                _solutions.Inject(uid, solution, solution);
            //TODO
        }
    }
}

