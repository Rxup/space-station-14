using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Backmen.Disease.Events;

[Serializable, NetSerializable]
public sealed partial class VaccineDoAfterEvent : SimpleDoAfterEvent
{
}
