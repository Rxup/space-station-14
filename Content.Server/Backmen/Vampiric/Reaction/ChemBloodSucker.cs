using Content.Server.Body.Systems;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Vampiric.Reaction;

public sealed partial class ChemBloodSucker : ReagentEffect
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => Loc.GetString("reagent-effect-guidebook-missing", ("chance", Probability));

    public override void Effect(ReagentEffectArgs args)
    {
        if (args.Scale < 1f)
            return;

        args.EntityManager.SystemOrNull<BloodSuckerSystem>()?.ConvertToVampire(args.SolutionEntity);
    }
}
