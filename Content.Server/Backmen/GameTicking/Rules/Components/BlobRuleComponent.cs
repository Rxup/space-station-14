using Content.Server.Backmen.Blob;
using Content.Shared.Mind;
using Robust.Shared.Audio;

namespace Content.Server.Backmen.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(BlobRuleSystem), typeof(BlobCoreSystem), typeof(BlobObserverSystem))]
public sealed partial class BlobRuleComponent : Component
{
    public List<(EntityUid mindId, MindComponent mind)> Blobs = new(); //BlobRoleComponent

    public BlobStage Stage = BlobStage.Default;

    [DataField]
    public SoundSpecifier? AlertAudio = new SoundPathSpecifier("/Audio/Announcements/attention.ogg");

    public float Accumulator = 0f;
}


public enum BlobStage : byte
{
    Default,
    Begin,
    Critical,
    TheEnd,
}
