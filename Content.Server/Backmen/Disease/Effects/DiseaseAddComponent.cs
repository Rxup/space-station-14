using Content.Shared.Backmen.Disease;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Server.Backmen.Disease.Effects;

/// <summary>
/// Adds a component to the diseased entity
/// </summary>
[UsedImplicitly]
public sealed partial class DiseaseAddComponent : DiseaseEffect
{
    /// <summary>
    /// The component that is added at the end of build up
    /// </summary>
    [DataField("components")]
    public ComponentRegistry Components = new();

    public override object GenerateEvent(Entity<DiseaseCarrierComponent> ent, ProtoId<DiseasePrototype> disease)
    {
        return new DiseaseEffectArgs<DiseaseAddComponent>(ent, disease, this);
    }
}

public sealed partial class DiseaseEffectSystem
{
    [Dependency] private readonly ISerializationManager _serialization = default!;

    private void DiseaseAddComponent(Entity<DiseaseCarrierComponent> ent, ref DiseaseEffectArgs<DiseaseAddComponent> args)
    {
        if(args.Handled)
            return;
        args.Handled = true;

        if (args.DiseaseEffect.Components.Count == 0)
            return;

        var uid = args.DiseasedEntity;

        foreach (var compReg in args.DiseaseEffect.Components.Values)
        {
            var compType = compReg.Component.GetType();

            if (HasComp(uid, compType))
                continue;
            var comp = (Component) _serialization.CreateCopy(compReg.Component, notNullableOverride: true);
            AddComp(uid, comp, true);
        }
    }
}
