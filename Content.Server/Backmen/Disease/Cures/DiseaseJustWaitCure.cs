using Content.Shared.Backmen.Disease;

namespace Content.Server.Backmen.Disease.Cures;

/// <summary>
/// Automatically removes the disease after a
/// certain amount of time.
/// </summary>
public sealed partial class DiseaseJustWaitCure : DiseaseCure
{
    /// <summary>
    /// All of these are in seconds
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public int Ticker = 0;
    [DataField("maxLength", required: true)]
    [ViewVariables(VVAccess.ReadWrite)]
    public int MaxLength = 150;

    public override string CureText()
    {
        return Loc.GetString("diagnoser-cure-wait", ("time", MaxLength));
    }
}

public sealed partial class DiseaseCureSystem
{
    private void DiseaseJustWaitCure(DiseaseCureArgs args, DiseaseJustWaitCure ds)
    {
        ds.Ticker++;
        if (ds.Ticker >= ds.MaxLength)
        {
            _disease.CureDisease(args.DiseasedEntity, args.Disease);
        }
    }
}
