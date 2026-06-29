using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;

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
