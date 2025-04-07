using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Backmen.Abilities.Psionics;

[RegisterComponent]
public sealed partial class ClothingGrantPsionicPowerComponent : Component
{
    [DataField("power", required: true, customTypeSerializer:typeof(ComponentNameSerializer))]
    public string Power;
    public bool IsActive = false;
}
