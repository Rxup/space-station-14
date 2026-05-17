using Content.Server.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Server.Backmen.Species.Shadowkin.Components;

[RegisterComponent]
public sealed partial class ShadowkinDarkSwapPowerComponent : Component
{
    /// <summary>
    ///     Factions temporarily deleted from the entity while swapped
    /// </summary>
    public List<ProtoId<NpcFactionPrototype>> SuppressedFactions = new();

    /// <summary>
    ///     Factions temporarily added to the entity while swapped
    /// </summary>
    [DataField("factions")]
    public List<ProtoId<NpcFactionPrototype>> AddedFactions = ["ShadowkinDarkFriendly"];

    public EntityUid? ShadowkinDarkSwapAction;
}
