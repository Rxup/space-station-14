using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Surgery.Traumas;

[Serializable, NetSerializable]
public enum TraumaType
{
    BoneDamage,
    OrganDamage,
    VeinsDamage,
    NerveDamage, // pain
    Dismemberment,
}

#region Bones

[Serializable, NetSerializable]
public enum BoneSeverity
{
    Normal,
    Damaged,
    Broken, // Ha-ha.
}

[ByRefEvent]
public record struct BoneSeverityPointChangedEvent(EntityUid Bone, BoneComponent BoneComponent, FixedPoint2 CurrentSeverity, FixedPoint2 SeverityDelta);

[ByRefEvent]
public record struct BoneSeverityChangedEvent(EntityUid Bone, BoneSeverity NewSeverity);

#endregion
