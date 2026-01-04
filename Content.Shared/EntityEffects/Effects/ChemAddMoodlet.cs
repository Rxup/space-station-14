using Content.Shared.Backmen.Mood;
using Content.Shared.EntityEffects;
using Content.Shared.Mobs.Components;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server.Chemistry.ReagentEffects;

/// <summary>
///     Adds a moodlet to an entity.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
[UsedImplicitly]
public sealed partial class ChemAddMoodletEntityEffectSystem : EntityEffectSystem<MobStateComponent, ChemAddMoodlet>
{
    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<ChemAddMoodlet> args)
    {
        var ev = new MoodEffectEvent(args.Effect.MoodPrototype);
        RaiseLocalEvent(entity, ev);
    }
}

[UsedImplicitly]
public sealed partial class ChemAddMoodlet : EntityEffectBase<ChemAddMoodlet>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        if (!prototype.TryIndex(MoodPrototype, out MoodEffectPrototype? moodProto))
            return null;
        
        return Loc.GetString("reagent-effect-guidebook-add-moodlet",
            ("amount", moodProto.MoodChange),
            ("timeout", moodProto.Timeout));
    }

    /// <summary>
    ///     The mood prototype to be applied to the using entity.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<MoodEffectPrototype> MoodPrototype = default!;
}
