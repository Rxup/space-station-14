using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Disease;

[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class DiseaseCure
{
    /// <summary>
    /// What stages the cure applies to.
    /// probably should be all, but go wild
    /// </summary>
    [DataField("stages")]
    public HashSet<int> Stages { get; private set; } = [0];

    /// <summary>
    /// This is used by the disease diangoser machine
    /// to generate reports to tell people all of a disease's
    /// special cures using in-game methods.
    /// So it should return a localization string describing
    /// the cure
    /// </summary>
    public abstract string CureText();

    public abstract object GenerateEvent(Entity<DiseaseCarrierComponent> ent, ProtoId<DiseasePrototype> disease);
}

public sealed class DiseaseCureArgs<T>(
    Entity<DiseaseCarrierComponent> diseasedEntity,
    ProtoId<DiseasePrototype> disease,
    T diseaseCure)
    : DiseaseArgs(diseasedEntity, disease)  where T : DiseaseCure
{
    public readonly T DiseaseCure = diseaseCure;
}
