using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Companions;

/// <summary>
/// Spawns a companion entity at this entity's position on map init, then deletes itself.
/// Used for uplink purchases that should immediately summon a familiar.
/// </summary>
[RegisterComponent]
public sealed partial class SpawnCompanionOnMapInitComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Companion;
}
