using Content.Shared.Backmen.Mood;
using Content.Shared.EntityEffects;
using Content.Shared.Backmen.Mood;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server.Chemistry.ReagentEffects;

/// <summary>
///     Adds a moodlet to an entity.
/// </summary>
[UsedImplicitly]
public sealed partial class ChemAddMoodlet : EntityEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var protoMan = IoCManager.Resolve<IPrototypeManager>();
        return Loc.GetString("reagent-effect-guidebook-add-moodlet",
            ("amount", protoMan.Index<MoodEffectPrototype>(MoodPrototype.Id).MoodChange),
            ("timeout", protoMan.Index<MoodEffectPrototype>(MoodPrototype.Id).Timeout));
    }

    public override void Effect(EntityEffectBaseArgs args)
    {
        var entityManager = IoCManager.Resolve<EntityManager>();
        var ev = new MoodEffectEvent(MoodPrototype);
        entityManager.EventBus.RaiseLocalEvent(args.TargetEntity, ev);
    }

    /// <summary>
    ///     The mood prototype to be applied to the using entity.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<MoodEffectPrototype> MoodPrototype = default!;
}
