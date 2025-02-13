using Content.Server.Body.Components;
using Content.Shared.Backmen.Disease;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

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

    public override object GenerateEvent(Entity<DiseaseCarrierComponent> ent, ProtoId<DiseasePrototype> disease)
    {
        return new DiseaseEffectArgs<DiseaseAdjustReagent>(ent, disease, this);
    }
}

public sealed partial class DiseaseEffectSystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    private void DiseaseAdjustReagent(Entity<DiseaseCarrierComponent> ent, ref DiseaseEffectArgs<DiseaseAdjustReagent> args)
    {
        if(args.Handled)
            return;

        args.Handled = true;

        if (args.DiseaseEffect.Reagent == null)
            return;

        if (!TryComp<BloodstreamComponent>(args.DiseasedEntity, out var bloodstream))
            return;

        var stream = bloodstream.ChemicalSolution;
        if (stream == null)
            return;

        if (args.DiseaseEffect.Amount < 0 && stream.Value.Comp.Solution.ContainsReagent(args.DiseaseEffect.Reagent.Value))
            _solutionContainer.RemoveReagent(stream.Value,args.DiseaseEffect.Reagent.Value, -args.DiseaseEffect.Amount);
        if (args.DiseaseEffect.Amount > 0)
            _solutionContainer.TryAddReagent(stream.Value, args.DiseaseEffect.Reagent.Value, args.DiseaseEffect.Amount, out _);
    }
}
