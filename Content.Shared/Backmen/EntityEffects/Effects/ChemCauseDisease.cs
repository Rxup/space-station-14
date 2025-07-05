using Content.Shared.Backmen.Disease;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Backmen.EntityEffects.Effects;

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
    [DataField]
    public float CauseChance = 0.15f;

    /// <summary>
    /// The disease to add.
    /// </summary>
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<DiseasePrototype>), required: true)]
    public string Disease;

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is EntityEffectReagentArgs reagentArgs && reagentArgs.Scale != 1f)
            return;

        args.EntityManager.System<SharedDiseaseSystem>().TryAddDisease(args.TargetEntity, Disease);
    }
}
