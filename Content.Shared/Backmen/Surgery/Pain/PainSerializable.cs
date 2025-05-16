using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Surgery.Pain;

[Serializable, NetSerializable]
public enum PainDamageTypes
{
    WoundPain,
    TraumaticPain,
}

[Serializable, NetSerializable]
public enum PainThresholdTypes
{
    None,
    PainGrunt,
    PainFlinch,
    Agony,
    PainShock,
    PainShockAndAgony,
}

[ByRefEvent]
public record struct BeforePainSoundPlayed(Entity<NerveSystemComponent> NerveSystem, SoundSpecifier PainAudio, bool Cancelled = false);

[Serializable, NetSerializable]
public sealed class KillAllPainSoundsEvent(NetEntity? nerveSys) : EntityEventArgs
{
    public NetEntity? NerveSystem { get; } = nerveSys;
}

[Serializable, NetSerializable]
public sealed class PlayPainSoundEvent(SoundSpecifier audio, NetEntity? source, AudioParams? audioParams) : EntityEventArgs
{
    public SoundSpecifier Audio { get; } = audio;

    public NetEntity? Source { get; } = source;
    public AudioParams? AudioParams { get; } = audioParams;
}

[Serializable, NetSerializable]
public sealed class PlayLoggedPainSoundEvent(NetEntity nerveSystem, SoundSpecifier audio, NetEntity? source, AudioParams? audioParams) : EntityEventArgs
{
    public NetEntity NerveSystem { get; } = nerveSystem;

    public SoundSpecifier Audio { get; } = audio;

    public NetEntity? Source { get; } = source;
    public AudioParams? AudioParams { get; } = audioParams;
}

[Serializable, NetSerializable]
public sealed class NerveComponentState : ComponentState
{
    public FixedPoint2 PainMultiplier;

    public Dictionary<(NetEntity, string), PainFeelingModifier> PainFeelingModifiers = new();

    public NetEntity ParentedNerveSystem;
}

[Serializable, DataRecord]
public record struct PainMultiplier(FixedPoint2 Change, string Identifier = "Unspecified", PainDamageTypes PainDamageType = PainDamageTypes.WoundPain, TimeSpan? Time = null);

[Serializable, DataRecord]
public record struct PainFeelingModifier(FixedPoint2 Change, TimeSpan? Time = null);

[Serializable, DataRecord]
public record struct PainModifier(FixedPoint2 Change, string Identifier = "Unspecified", PainDamageTypes PainDamageType = PainDamageTypes.WoundPain, TimeSpan? Time = null); // Easier to manage pain with modifiers.

[ByRefEvent]
public record struct PainThresholdTriggered(Entity<NerveSystemComponent> NerveSystem, PainThresholdTypes ThresholdType, FixedPoint2 PainInput, bool Cancelled = false);

[ByRefEvent]
public record struct PainThresholdEffected(Entity<NerveSystemComponent> NerveSystem, PainThresholdTypes ThresholdType, FixedPoint2 PainInput);

[ByRefEvent]
public record struct PainFeelsChangedEvent(EntityUid NerveSystem, EntityUid NerveEntity, FixedPoint2 CurrentPainFeels);

[ByRefEvent]
public record struct PainModifierAddedEvent(EntityUid NerveSystem, EntityUid NerveUid, FixedPoint2 AddedPain);

[ByRefEvent]
public record struct PainModifierRemovedEvent(EntityUid NerveSystem, EntityUid NerveUid, FixedPoint2 CurrentPain);

[ByRefEvent]
public record struct PainModifierChangedEvent(EntityUid NerveSystem, EntityUid NerveUid, FixedPoint2 CurrentPain);
