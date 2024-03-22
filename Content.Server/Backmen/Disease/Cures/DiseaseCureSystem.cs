using Content.Shared.Backmen.Disease;

namespace Content.Server.Backmen.Disease.Cures;

public sealed partial class DiseaseCureSystem : EntitySystem
{
    [Dependency] private readonly DiseaseSystem _disease = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiseaseCureArgs>(DoGenericCure);
    }

    private void DoGenericCure(DiseaseCureArgs args)
    {
        if(args.Handled)
            return;

        switch (args.DiseaseCure)
        {
            case DiseaseBedrestCure ds1:
                args.Handled = true;
                DiseaseBedrestCure(args, ds1);
                return;
            case DiseaseBodyTemperatureCure ds2:
                args.Handled = true;
                DiseaseBodyTemperatureCure(args, ds2);
                return;
            case DiseaseJustWaitCure ds3:
                args.Handled = true;
                DiseaseJustWaitCure(args, ds3);
                return;
            case DiseaseReagentCure ds4:
                args.Handled = true;
                DiseaseReagentCure(args, ds4);
                return;
        }
    }
}
