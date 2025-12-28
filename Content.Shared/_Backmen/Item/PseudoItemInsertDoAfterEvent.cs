using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Backmen.Item;

[Serializable, NetSerializable]
public sealed partial class PseudoItemInsertDoAfterEvent : SimpleDoAfterEvent
{
}
