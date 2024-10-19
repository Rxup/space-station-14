using Content.Server.Backmen.Disease;
using Content.Shared.Backmen.Disease;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Backmen.Chemistry.ReagentEffects;

/// <summary>
/// Default metabolism for medicine reagents.
/// </summary>
[UsedImplicitly]
public sealed partial class ChemCauseDisease : EntityEffect
{
    public override bool ShouldLog => true;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-chem-cause-disease", ("chance", Probability),
            ("disease", prototype.Index<DiseasePrototype>(Disease).Name));

    /// <summary>
    /// Chance it has each tick to cause disease, between 0 and 1
    /// </summary>
    [DataField("causeChance")]
    public float CauseChance = 0.15f;

    /// <summary>
    /// The disease to add.
    /// </summary>
    [DataField("disease", customTypeSerializer: typeof(PrototypeIdSerializer<DiseasePrototype>), required: true)]
    [ViewVariables(VVAccess.ReadWrite)]
    public string Disease = default!;

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is EntityEffectReagentArgs reagentArgs && reagentArgs.Scale != 1f)
            return;

        args.EntityManager.System<DiseaseSystem>().TryAddDisease(args.TargetEntity, Disease);
    }
}
