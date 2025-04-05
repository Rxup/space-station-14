using Content.Shared.Actions;
using Content.Shared.Item;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Drone.Actions;

public sealed partial class DroneCraftActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public EntProtoId<ItemComponent> CraftedItem { get; set; }

    [DataField]
    public float Energy { get; set; } = 10;
}
