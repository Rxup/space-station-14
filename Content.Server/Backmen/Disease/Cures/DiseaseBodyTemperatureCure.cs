using Content.Server.Temperature.Components;
using Content.Shared.Backmen.Disease;

namespace Content.Server.Backmen.Disease.Cures;

public sealed partial class DiseaseBodyTemperatureCure : DiseaseCure
{
    [DataField("min")]
    public float Min = 0;

    [DataField("max")]
    public float Max = float.MaxValue;

    public override string CureText()
    {
        if (Min == 0)
            return Loc.GetString("diagnoser-cure-temp-max", ("max", Math.Round(Max)));
        if (Max == float.MaxValue)
            return Loc.GetString("diagnoser-cure-temp-min", ("min", Math.Round(Min)));

        return Loc.GetString("diagnoser-cure-temp-both", ("max", Math.Round(Max)), ("min", Math.Round(Min)));
    }
}

public sealed partial class DiseaseCureSystem
{
    private void DiseaseBodyTemperatureCure(DiseaseCureArgs args, DiseaseBodyTemperatureCure ds)
    {
        if (!TryComp<TemperatureComponent>(args.DiseasedEntity, out var temp))
            return;

        if(temp.CurrentTemperature > ds.Min && temp.CurrentTemperature < float.MaxValue)
        {
            _disease.CureDisease(args.DiseasedEntity, args.Disease);
        }
    }
}
