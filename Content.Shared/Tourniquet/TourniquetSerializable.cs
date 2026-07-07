using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Tourniquet;

[Serializable, NetSerializable]
public sealed partial class TourniquetDoAfterEvent : SimpleDoAfterEvent
{
    // start-backmen: medical-targeting
    public NetEntity TargetWoundable = NetEntity.Invalid;
    // end-backmen: medical-targeting
}

[Serializable, NetSerializable]
public sealed partial class RemoveTourniquetDoAfterEvent : SimpleDoAfterEvent
{
}
