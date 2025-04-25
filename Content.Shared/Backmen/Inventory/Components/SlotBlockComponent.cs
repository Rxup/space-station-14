using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Inventory;

/// <summary>
/// Used to prevent items from being unequipped and equipped from slots that are listed in <see cref="Slots"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SlotBlockSystem))]
public sealed partial class SlotBlockComponent : Component
{
    /// <summary>
    /// Slots that this entity should block.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public SlotFlags Slots = SlotFlags.NONE;
}
