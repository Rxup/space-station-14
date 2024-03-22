using Content.Server.Body.Components;
using Content.Shared.Backmen.Disease;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Disease.Cures;

/// <summary>
/// Cures the disease if a certain amount of reagent
/// is in the host's chemstream.
/// </summary>
public sealed partial class DiseaseReagentCure : DiseaseCure
{
    [DataField("min")]
    public FixedPoint2 Min = 5;
    [DataField("reagent")]
    public ReagentId? Reagent;

    public override string CureText()
    {
        var prototypeMan = IoCManager.Resolve<IPrototypeManager>();
        if (Reagent == null || !prototypeMan.TryIndex<ReagentPrototype>(Reagent.Value.Prototype, out var reagentProt))
            return string.Empty;
        return (Loc.GetString("diagnoser-cure-reagent", ("units", Min), ("reagent", reagentProt.LocalizedName)));
    }
}

public sealed partial class DiseaseCureSystem
{
    private void DiseaseReagentCure(DiseaseCureArgs args, DiseaseReagentCure ds)
    {
        if (!TryComp<BloodstreamComponent>(args.DiseasedEntity, out var bloodstream)
            || bloodstream.ChemicalSolution == null)
            return;

        var chemicalSolution = bloodstream.ChemicalSolution.Value;

        var quant = FixedPoint2.Zero;
        if (ds.Reagent != null && chemicalSolution.Comp.Solution.ContainsReagent(ds.Reagent.Value))
        {
            quant = chemicalSolution.Comp.Solution.GetReagentQuantity(ds.Reagent.Value);
        }

        if (quant >= ds.Min)
        {
            _disease.CureDisease(args.DiseasedEntity, args.Disease);
        }
    }
}
