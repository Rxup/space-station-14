using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Language.Events;

/// <summary>
/// Raised after the doafter is completed when using the item.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class LanguageLearnDoAfterEvent : SimpleDoAfterEvent{}
