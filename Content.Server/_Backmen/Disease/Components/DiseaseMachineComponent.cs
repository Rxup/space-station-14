using Content.Shared._Backmen.Disease;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server._Backmen.Disease.Components;

/// <summary>
/// For shared behavior between both disease machines
/// </summary>
[RegisterComponent]
public sealed partial class DiseaseMachineComponent : Component
{
    [DataField("delay")]
    public float Delay = 5f;
    /// <summary>
    /// How much time we've accumulated processing
    /// </summary>
    [DataField("accumulator")]
    public float Accumulator = 0f;

    /// <summary>
    /// Prototypes queued.
    /// </summary>
    public int Queued = 0;

    /// <summary>
    /// The disease prototype currently being diagnosed
    /// </summary>
    [ViewVariables]
    public DiseasePrototype? Disease;
    /// <summary>
    /// What the machine will spawn
    /// </summary>
    [DataField("machineOutput", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>), required: true)]
    public string MachineOutput = string.Empty;
}
