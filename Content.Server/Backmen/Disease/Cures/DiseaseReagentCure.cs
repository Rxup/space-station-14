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

    [ViewVariables]
    public FixedPoint2 MetabolizedAmount = FixedPoint2.Zero;

    public override string CureText()
    {
        var prototypeMan = IoCManager.Resolve<IPrototypeManager>();
        if (Reagent == null || !prototypeMan.TryIndex<ReagentPrototype>(Reagent.Value.Prototype, out var reagentProt))
            return string.Empty;
        return (Loc.GetString("diagnoser-cure-reagent", ("units", Min), ("reagent", reagentProt.LocalizedName)));
    }

    public override object GenerateEvent(Entity<DiseaseCarrierComponent> ent, ProtoId<DiseasePrototype> disease)
    {
        return new DiseaseCureArgs<DiseaseReagentCure>(ent, disease, this);
    }
}

public sealed partial class DiseaseCureSystem
{
    private void DiseaseReagentCure(Entity<DiseaseCarrierComponent> ent, ref DiseaseCureArgs<DiseaseReagentCure> args)
    {
        if(args.Handled)
            return;

        args.Handled = true;

        if (args.DiseaseCure.MetabolizedAmount >= args.DiseaseCure.Min)
        {
            _disease.CureDisease(args.DiseasedEntity, args.Disease);
        }
    }
}
