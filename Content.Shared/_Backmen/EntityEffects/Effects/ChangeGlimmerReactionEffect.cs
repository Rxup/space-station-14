using Content.Shared._Backmen.Psionics.Glimmer;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Backmen.EntityEffects.Effects;

[DataDefinition]
public sealed partial class ChangeGlimmerReactionEffect : EntityEffect
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-change-glimmer-reaction-effect", ("chance", Probability),
            ("count", Count));

    /// <summary>
    ///     Added to glimmer when reaction occurs.
    /// </summary>
    [DataField("count")]
    public int Count = 1;

    public override void Effect(EntityEffectBaseArgs args)
    {
        var glimmersys = args.EntityManager.EntitySysManager.GetEntitySystem<GlimmerSystem>();

        glimmersys.Glimmer += Count;
    }
}
