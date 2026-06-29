using Content.Server.Backmen.GameTicking.Rules.Components;

namespace Content.Server.Backmen.Blob;

public sealed class BlobChangeLevelEvent : EntityEventArgs
{
    public EntityUid Station;
    public BlobStage Level;
}
