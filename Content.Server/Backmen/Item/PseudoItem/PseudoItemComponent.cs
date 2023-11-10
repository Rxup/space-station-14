using Content.Shared.Item;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Item.PseudoItem;

/// <summary>
/// For entities that behave like an item under certain conditions,
/// but not under most conditions.
/// </summary>
[RegisterComponent]
public sealed partial class PseudoItemComponent : Component
{
    [DataField("size")]
    public ProtoId<ItemSizePrototype> Size = "Huge";

    [DataField("sizeInBackpack")]
    public ProtoId<ItemSizePrototype> SizeInBackpack = "Felinid";

    public bool Active = false;
}
