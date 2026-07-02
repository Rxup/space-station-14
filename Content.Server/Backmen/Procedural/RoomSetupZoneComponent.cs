using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Procedural;

/// <summary>
/// Tracks a procedural room until setup requirements are fulfilled.
/// Atmos devices tagged with <see cref="ActivationTag"/> stay disabled until then.
/// </summary>
[RegisterComponent]
public sealed partial class RoomSetupZoneComponent : Component
{
    [DataField(required: true)]
    public ProtoId<TagPrototype> ActivationTag;

    [DataField]
    public RoomSetupRequirements Requirements = RoomSetupRequirements.Canisters;

    [DataField]
    public int RequiredCanisterPorts = 2;

    [DataField]
    public bool Activated;

    [ViewVariables]
    public EntityUid? GridUid;

    [ViewVariables]
    public Box2i Bounds;

    [ViewVariables]
    public HashSet<EntityUid> PortEntities = new();
}
