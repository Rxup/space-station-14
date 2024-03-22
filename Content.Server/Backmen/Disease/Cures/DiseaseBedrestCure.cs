using Content.Server.Bed.Components;
using Content.Shared.Backmen.Disease;
using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle.Components;

namespace Content.Server.Backmen.Disease.Cures;

public sealed partial class DiseaseBedrestCure : DiseaseCure
{
    [ViewVariables(VVAccess.ReadWrite)]
    public int Ticker = 0;

    /// How many extra ticks you get for sleeping.
    [DataField("sleepMultiplier")]
    public int SleepMultiplier = 3;

    [DataField("maxLength", required: true)]
    [ViewVariables(VVAccess.ReadWrite)]
    public int MaxLength = 60;

    public override string CureText()
    {
        return (Loc.GetString("diagnoser-cure-bedrest", ("time", MaxLength), ("sleep", (MaxLength / SleepMultiplier))));
    }
}

public sealed partial class DiseaseCureSystem
{
    private void DiseaseBedrestCure(DiseaseCureArgs args, DiseaseBedrestCure ds)
    {
        if (!TryComp<BuckleComponent>(args.DiseasedEntity, out var buckle) ||
            !HasComp<HealOnBuckleComponent>(buckle.BuckledTo))
            return;

        var ticks = 1;
        if (HasComp<SleepingComponent>(args.DiseasedEntity))
            ticks *= ds.SleepMultiplier;

        if (buckle.Buckled)
            ds.Ticker += ticks;
        if (ds.Ticker >= ds.MaxLength)
        {
            _disease.CureDisease(args.DiseasedEntity, args.Disease);
        }
    }
}
