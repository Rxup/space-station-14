using Content.Shared._Backmen.Blob.Components;
using Content.Shared._Backmen.Blob.NPC.BlobPod;

namespace Content.Client._Backmen.Blob;

public sealed class BlobPodSystem : SharedBlobPodSystem
{
    public override bool NpcStartZombify(EntityUid uid, EntityUid argsTarget, BlobPodComponent component)
    {
        // do nothing
        return false;
    }
}
