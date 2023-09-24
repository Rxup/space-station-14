using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Item;

[Serializable, NetSerializable]
public sealed partial class PseudoItemInsertDoAfterEvent : SimpleDoAfterEvent
{
}
