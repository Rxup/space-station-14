using Content.Server._Backmen.GameTicking.Rules.Components;
using Content.Shared._Backmen.Blob.Components;

namespace Content.Server._Backmen.Blob;

public sealed class BlobChangeLevelEvent : EntityEventArgs
{
    public EntityUid Station;
    public BlobStage Level;
}
