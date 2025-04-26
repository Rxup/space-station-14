using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Inventory;

/// <summary>
/// Used to prevent items from being unequipped and equipped from slots that are listed in <see cref="Slots"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(CoveredSlotSystem))]
public sealed partial class CoveredSlotComponent : Component
{
    /// <summary>
    /// Slots that this entity should cover.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public SlotFlags Slots = SlotFlags.NONE;
}
