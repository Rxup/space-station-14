using Content.Server.NPC.Components;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Server.Backmen.Species.Shadowkin.Components;

[RegisterComponent]
public sealed partial class ShadowkinDarkSwapPowerComponent : Component
{
    /// <summary>
    ///     Factions temporarily deleted from the entity while swapped
    /// </summary>
    public List<string> SuppressedFactions = new();

    /// <summary>
    ///     Factions temporarily added to the entity while swapped
    /// </summary>
    [DataField("factions", customTypeSerializer: typeof(PrototypeIdListSerializer<NpcFactionPrototype>))]
    public List<string> AddedFactions = new() { "ShadowkinDarkFriendly" };

    public EntityUid? ShadowkinDarkSwapAction;
}
