using Content.Server.Body.Systems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Vampiric.Reaction;

public sealed partial class ChemBloodSucker  : EntityEffect
{
    public override bool ShouldLog => true;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => Loc.GetString("reagent-effect-guidebook-missing", ("chance", Probability));

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is EntityEffectReagentArgs reagentArgs && reagentArgs.Scale < 1f)
            return;

        args.EntityManager.SystemOrNull<BloodSuckerSystem>()?.ConvertToVampire(args.TargetEntity);
    }
}
