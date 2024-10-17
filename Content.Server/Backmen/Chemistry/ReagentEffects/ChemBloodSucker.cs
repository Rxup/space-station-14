using Content.Server.Backmen.Vampiric;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Chemistry.ReagentEffects;

public sealed partial class ChemBloodSucker : EntityEffect
{
    public override bool ShouldLog => true;

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => Loc.GetString("reagent-effect-guidebook-missing", ("chance", Probability));

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is EntityEffectReagentArgs reagentArgs && reagentArgs.Scale < 1f)
            return;

        args.EntityManager.SystemOrNull<BloodSuckerSystem>()?.ForceMakeVampire(args.TargetEntity);
    }
}
