using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Clothing;

[RegisterComponent]
public sealed partial class ClothingGrantComponent : Component
{
    [DataField("component", required: true)]
    [AlwaysPushInheritance]
    public ComponentRegistry Components { get; private set; } = new();

    [ViewVariables(VVAccess.ReadWrite)]
    public bool IsActive = false;
}
