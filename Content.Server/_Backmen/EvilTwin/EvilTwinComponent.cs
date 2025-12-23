using Content.Shared.Mind;

namespace Content.Server._Backmen.EvilTwin;

[RegisterComponent]
public sealed partial class EvilTwinComponent : Component
{
    public EntityUid? TwinMindId;
    public MindComponent? TwinMind;
    public EntityUid? TwinEntity;
}
