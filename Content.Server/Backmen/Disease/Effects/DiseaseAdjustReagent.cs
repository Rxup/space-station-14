using Content.Server.Body.Components;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Shared.Backmen.Disease;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Backmen.Disease.Effects;

/// <summary>
/// Adds or removes reagents from the
/// host's chemstream.
/// </summary>
[UsedImplicitly]
public sealed partial class DiseaseAdjustReagent : DiseaseEffect
{
    /// <summary>
    ///     The reagent ID to add or remove.
    /// </summary>
    [DataField("reagent")]
    public ReagentId? Reagent = null;

    [DataField("amount", required: true)]
    public FixedPoint2 Amount = default!;
}

public sealed partial class DiseaseEffectSystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    private void DiseaseAdjustReagent(DiseaseEffectArgs args, DiseaseAdjustReagent ds)
    {
        if (ds.Reagent == null)
            return;

        if (!TryComp<BloodstreamComponent>(args.DiseasedEntity, out var bloodstream))
            return;

        var stream = bloodstream.ChemicalSolution;
        if (stream == null)
            return;

        if (ds.Amount < 0 && stream.Value.Comp.Solution.ContainsReagent(ds.Reagent.Value))
            _solutionContainer.RemoveReagent(stream.Value,ds.Reagent.Value, -ds.Amount);
        if (ds.Amount > 0)
            _solutionContainer.TryAddReagent(stream.Value, ds.Reagent.Value, ds.Amount, out _);
    }
}
