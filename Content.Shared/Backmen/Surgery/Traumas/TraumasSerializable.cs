﻿using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Body.Organ;
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

[ByRefEvent]
public record struct TraumaChanceDeductionEvent(FixedPoint2 TraumaSeverity, TraumaType TraumaType, FixedPoint2 ChanceDeduction);

[ByRefEvent]
public record struct BeforeTraumaInducedEvent(FixedPoint2 TraumaSeverity, EntityUid TraumaTarget, TraumaType TraumaType, bool Cancelled = false);

[ByRefEvent]
public record struct TraumaInducedEvent(Entity<TraumaComponent> Trauma, EntityUid TraumaTarget, FixedPoint2 TraumaSeverity, TraumaType TraumaType);

#region Organs

[Serializable, NetSerializable]
public enum OrganSeverity
{
    Normal,
    Damaged,
    Destroyed, // obliterated
}

[ByRefEvent]
public record struct OrganIntegrityChangedEvent(FixedPoint2 OldIntegrity, FixedPoint2 NewIntegrity);

[ByRefEvent]
public record struct OrganDamageSeverityChanged(OrganSeverity OldSeverity, OrganSeverity NewSeverity);

[ByRefEvent]
public record struct OrganIntegrityChangedEventOnWoundable(Entity<OrganComponent> Organ, FixedPoint2 OldIntegrity, FixedPoint2 NewIntegrity);

[ByRefEvent]
public record struct OrganDamageSeverityChangedOnWoundable(Entity<OrganComponent> Organ, OrganSeverity OldSeverity, OrganSeverity NewSeverity);


#endregion

#region Bones

[Serializable, NetSerializable]
public enum BoneSeverity
{
    Normal,
    Damaged,
    Broken, // Ha-ha.
}

[ByRefEvent]
public record struct BoneIntegrityChangedEvent(Entity<BoneComponent> Bone, FixedPoint2 OldIntegrity, FixedPoint2 NewIntegrity);

[ByRefEvent]
public record struct BoneSeverityChangedEvent(Entity<BoneComponent> Bone, BoneSeverity OldSeverity, BoneSeverity NewSeverity);

#endregion
