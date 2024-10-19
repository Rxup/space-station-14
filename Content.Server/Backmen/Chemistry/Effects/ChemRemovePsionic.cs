using Content.Shared.Chemistry.Reagent;
using Content.Server.Backmen.Abilities.Psionics;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Chemistry.ReagentEffects;

/// <summary>
/// Rerolls psionics once.
/// </summary>
[UsedImplicitly]
public sealed partial class ChemRemovePsionic : EntityEffect
{
    public override bool ShouldLog => true;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-chem-remove-psionic", ("chance", Probability));

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is EntityEffectReagentArgs reagentArgs && reagentArgs.Scale != 1f)
            return;

        var psySys = args.EntityManager.EntitySysManager.GetEntitySystem<PsionicAbilitiesSystem>();

        psySys.RemovePsionics(args.TargetEntity);
    }
}
