using Content.Shared.Backmen.Disease;
using Content.Shared.Chat.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Backmen.Disease.Effects;

/// <summary>
/// Makes the diseased sneeze or cough
/// or neither.
/// </summary>
[UsedImplicitly]
public sealed partial class DiseaseSnough : DiseaseEffect
{
    /// <summary>
    /// Emote to play when snoughing
    /// </summary>
    [DataField("emote", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<EmotePrototype>))]
    public string EmoteId = String.Empty;

    /// <summary>
    /// Whether to spread the disease through the air
    /// </summary>
    [DataField("airTransmit")]
    public bool AirTransmit = true;

    public override object GenerateEvent(Entity<DiseaseCarrierComponent> ent, ProtoId<DiseasePrototype> disease)
    {
        return new DiseaseEffectArgs<DiseaseSnough>(ent, disease, this);
    }
}
public sealed partial class DiseaseEffectSystem
{
    [Dependency] private readonly DiseaseSystem _disease = default!;
    private void DiseaseSnough(Entity<DiseaseCarrierComponent> ent, ref DiseaseEffectArgs<DiseaseSnough> args)
    {
        if(args.Handled)
            return;
        args.Handled = true;
        _disease.SneezeCough(args.DiseasedEntity, args.Disease, args.DiseaseEffect.EmoteId, args.DiseaseEffect.AirTransmit);
    }
}
