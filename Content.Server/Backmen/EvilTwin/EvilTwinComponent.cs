using Content.Server.Mind;

namespace Content.Server.Backmen.EvilTwin;

[RegisterComponent]
public sealed partial class EvilTwinComponent : Component
{
    public MindComponent? TwinMind;
    public EntityUid? TwinEntity;
}
