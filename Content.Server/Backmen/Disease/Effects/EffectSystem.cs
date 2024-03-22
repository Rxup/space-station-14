using Content.Shared.Backmen.Disease;
using Content.Shared.Backmen.Disease.Effects;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Disease.Effects;

public sealed partial class DiseaseEffectSystem : SharedDiseaseEffectSystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiseaseEffectArgs>(DoGenericEffect);
    }

    private void DoGenericEffect(DiseaseEffectArgs args)
    {
        if(args.Handled)
            return;

        switch (args.DiseaseEffect)
        {
            case DiseaseAddComponent ds1:
                args.Handled = true;
                DiseaseAddComponent(args, ds1);
                return;
            case DiseaseAdjustReagent ds2:
                args.Handled = true;
                DiseaseAdjustReagent(args, ds2);
                return;
            case DiseaseGenericStatusEffect ds3:
                args.Handled = true;
                DiseaseGenericStatusEffect(args, ds3);
                return;
            case DiseaseHealthChange ds4:
                args.Handled = true;
                DiseaseHealthChange(args, ds4);
                return;
            case DiseasePolymorph ds5:
                args.Handled = true;
                DiseasePolymorph(args, ds5);
                return;
            case DiseasePopUp ds6:
                args.Handled = true;
                DiseasePopUp(args, ds6);
                return;
            case DiseaseSnough ds7:
                args.Handled = true;
                DiseaseSnough(args, ds7);
                return;
            case DiseaseVomit ds8:
                args.Handled = true;
                DiseaseVomit(args, ds8);
                return;
        }
    }
}
