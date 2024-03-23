using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Disease;

[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class DiseaseEffect : HandledEntityEventArgs
{
    /// <summary>
    ///     What's the chance, from 0 to 1, that this effect will occur?
    /// </summary>
    [DataField("probability")]
    public float Probability = 1.0f;
    /// <summary>
    ///     What stages this effect triggers on
    /// </summary>
    [DataField("stages")]
    public HashSet<int> Stages { get; private set; } = [0];
    public abstract object GenerateEvent(Entity<DiseaseCarrierComponent> ent, ProtoId<DiseasePrototype> disease);
}

public abstract class DiseaseArgs(Entity<DiseaseCarrierComponent> diseasedEntity, ProtoId<DiseasePrototype> disease)
    : HandledEntityEventArgs
{
    public readonly Entity<DiseaseCarrierComponent> DiseasedEntity = diseasedEntity;
    public readonly ProtoId<DiseasePrototype> Disease = disease;
}

/// <summary>
/// What you have to work with in any disease effect/cure.
/// Includes an entity manager because it is out of scope
/// otherwise.
/// </summary>
public sealed class DiseaseEffectArgs<T>(
    Entity<DiseaseCarrierComponent> diseasedEntity,
    ProtoId<DiseasePrototype> disease,
    T diseaseEffect)
    : DiseaseArgs(diseasedEntity, disease) where T : DiseaseEffect
{
    public readonly T DiseaseEffect = diseaseEffect;
}
