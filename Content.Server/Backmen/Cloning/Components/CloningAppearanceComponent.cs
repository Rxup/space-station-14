using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Cloning.Components;

[RegisterComponent]
public sealed partial class CloningAppearanceComponent : Component
{
    [DataField("components")]
    [AlwaysPushInheritance]
    public ComponentRegistry Components { get; private set; } = new();

    [DataField("gear")]
    public ProtoId<StartingGearPrototype>? Gear;
}
