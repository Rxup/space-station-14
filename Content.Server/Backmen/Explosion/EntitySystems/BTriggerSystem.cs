using Content.Server.Backmen.Fluids.EntitySystems;
using Content.Server.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Fluids.EntitySystems;

namespace Content.Server.Backmen.Explosion.EntitySystems;

public sealed class BTriggerSystem : EntitySystem
{
    [Dependency] private readonly PuddleSystem _puddleSystem = default!;
    [Dependency] private readonly SolutionContainerSystem _solutionSystem = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SplashOnTriggerComponent, TriggerEvent>(OnSplashTrigger);
    }

    private void OnSplashTrigger(EntityUid uid, SplashOnTriggerComponent component, TriggerEvent args)
    {
        var xform = Transform(uid);

        var coords = xform.Coordinates;

        if (!coords.IsValid(EntityManager))
            return;

        var transferSolution = new Solution();
        foreach (var reagent in component.SplashReagents.ToArray())
        {
            transferSolution.AddReagent(reagent.Reagent, reagent.Quantity);
        }
        if (_solutionSystem.TryGetInjectableSolution(uid, out var injectableSolution))
        {
            _solutionSystem.TryAddSolution(uid, injectableSolution, transferSolution);
        }

        _puddleSystem.TrySplashSpillAt(uid, coords, transferSolution, out var puddleUid);
    }
}
