using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.ADT.Weapons.Ranged.Components;

/// <summary>
/// Позволяет оружию меха стрелять проджектайлами.
/// Использует батарею меха
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BatteryMechAmmoProviderComponent : MechAmmoProviderComponent
{
    [ViewVariables(VVAccess.ReadWrite), DataField("proto", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string Prototype = default!;

    [DataField("fireCost")]
    [AutoNetworkedField]
    public float ShotCost = 15f;
}
