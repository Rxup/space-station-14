using Content.Shared.Backmen.Disease;
using Content.Shared.StatusEffect;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Disease.Effects;

/// <summary>
/// Adds a generic status effect to the entity.
/// Differs from the chem version in its defaults
/// to better facilitate adding components that
/// last the length of the disease.
/// </summary>
[UsedImplicitly]
public sealed partial class DiseaseGenericStatusEffect : DiseaseEffect
{
    /// <summary>
    /// The status effect key
    /// Prevents other components from being with the same key
    /// </summary>
    [DataField("key", required: true)]
    public EntProtoId Key = default!;

    [DataField("time")]
    public float Time = 1.01f;

    /// I'm afraid if this was exact the key could get stolen by another thing
    /// <remarks>
    ///     true - refresh status effect time,  false - accumulate status effect time
    /// </remarks>
    [DataField("refresh")]
    public bool Refresh = false;

    /// <summary>
    ///     Should this effect add the status effect, remove time from it, or set its cooldown?
    /// </summary>
    [DataField("type")]
    public StatusEffectDiseaseType Type = StatusEffectDiseaseType.Add;

    public override object GenerateEvent(Entity<DiseaseCarrierComponent> ent, ProtoId<DiseasePrototype> disease)
    {
        return new DiseaseEffectArgs<DiseaseGenericStatusEffect>(ent, disease, this);
    }
}

/// See status effects for how these work
public enum StatusEffectDiseaseType
{
    Add,
    Remove,
    Set
}

public sealed partial class DiseaseEffectSystem
{
    [Dependency] private readonly Shared.StatusEffectNew.StatusEffectsSystem _effectsSystem = default!;

    private void DiseaseGenericStatusEffect(Entity<DiseaseCarrierComponent> ent, ref DiseaseEffectArgs<DiseaseGenericStatusEffect> args)
    {
        if(args.Handled)
            return;
        args.Handled = true;
        if (args.DiseaseEffect.Type == StatusEffectDiseaseType.Add)
        {
            if (args.DiseaseEffect.Refresh)
            {
                _effectsSystem.TryAddStatusEffectDuration(args.DiseasedEntity, args.DiseaseEffect.Key, TimeSpan.FromSeconds(args.DiseaseEffect.Time));
            }
            else
            {
                _effectsSystem.TrySetStatusEffectDuration(args.DiseasedEntity, args.DiseaseEffect.Key, TimeSpan.FromSeconds(args.DiseaseEffect.Time));
            }

        }
        else if (args.DiseaseEffect.Type == StatusEffectDiseaseType.Remove)
        {
            _effectsSystem.TryRemoveTime(args.DiseasedEntity, args.DiseaseEffect.Key, TimeSpan.FromSeconds(args.DiseaseEffect.Time));
        }
        else if (args.DiseaseEffect.Type == StatusEffectDiseaseType.Set)
        {
            _effectsSystem.TrySetTime(args.DiseasedEntity, args.DiseaseEffect.Key, TimeSpan.FromSeconds(args.DiseaseEffect.Time));
        }
    }
}
