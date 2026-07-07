using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Medical;

[Serializable, NetSerializable]
public sealed partial class HealingDoAfterEvent : SimpleDoAfterEvent
{
    // start-backmen: medical-targeting
    public NetEntity TargetWoundable = NetEntity.Invalid;
    // end-backmen: medical-targeting
}
