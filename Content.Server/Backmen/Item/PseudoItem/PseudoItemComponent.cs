using Content.Shared.Item;

namespace Content.Server.Backmen.Item.PseudoItem;

/// <summary>
/// For entities that behave like an item under certain conditions,
/// but not under most conditions.
/// </summary>
[RegisterComponent]
public sealed partial class PseudoItemComponent : Component
{
    [DataField("size")]
    public ItemSize Size = ItemSize.Huge;

    [DataField("sizeInBackpack")]
    public ItemSize SizeInBackpack = (ItemSize)28;

    public bool Active = false;
}
