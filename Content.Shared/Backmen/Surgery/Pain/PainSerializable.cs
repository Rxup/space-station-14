using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Surgery.Pain;

[Serializable, NetSerializable]
public enum PainType
{
    WoundPain,
    TraumaticPain,
    Starving,
}

[Serializable, NetSerializable]
public enum PainReflexType
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

[Serializable, DataDefinition]
public partial struct PainMultiplier
{
    [DataField]
    public FixedPoint2 Change;

    [DataField]
    public string Identifier = "Unspecified";

    [DataField]
    public PainType PainType = PainType.WoundPain;

    [DataField]
    public TimeSpan? Time;

    public PainMultiplier(FixedPoint2 Change, string Identifier = "Unspecified", PainType PainType = PainType.WoundPain, TimeSpan? Time = null)
    {
        this.Change = Change;
        this.Identifier = Identifier;
        this.PainType = PainType;
        this.Time = Time;
    }
}

[Serializable, DataDefinition]
public partial struct PainFeelingModifier
{
    [DataField]
    public FixedPoint2 Change;

    [DataField]
    public TimeSpan? Time;

    public PainFeelingModifier(FixedPoint2 Change, TimeSpan? Time = null)
    {
        this.Change = Change;
        this.Time = Time;
    }
}

[Serializable, DataDefinition]
public partial struct PainModifier
{
    [DataField]
    public FixedPoint2 Change;

    [DataField]
    public string Identifier = "Unspecified";

    [DataField]
    public PainType PainType = PainType.WoundPain;

    [DataField]
    public TimeSpan? Time;

    public PainModifier(FixedPoint2 Change, string Identifier = "Unspecified", PainType PainType = PainType.WoundPain, TimeSpan? Time = null)
    {
        this.Change = Change;
        this.Identifier = Identifier;
        this.PainType = PainType;
        this.Time = Time;
    }
}

[ByRefEvent]
public record struct PainThresholdTriggered(Entity<NerveSystemComponent> NerveSystem, PainReflexType ReflexType, FixedPoint2 PainInput, bool Cancelled = false);

[ByRefEvent]
public record struct PainThresholdEffected(Entity<NerveSystemComponent> NerveSystem, PainReflexType ReflexType, FixedPoint2 PainInput);

[ByRefEvent]
public record struct PainFeelsChangedEvent(EntityUid NerveSystem, EntityUid NerveEntity, FixedPoint2 CurrentPainFeels);

[ByRefEvent]
public record struct PainModifierAddedEvent(EntityUid NerveSystem, EntityUid NerveUid, FixedPoint2 AddedPain);

[ByRefEvent]
public record struct PainModifierRemovedEvent(EntityUid NerveSystem, EntityUid NerveUid, FixedPoint2 CurrentPain);

[ByRefEvent]
public record struct PainModifierChangedEvent(EntityUid NerveSystem, EntityUid NerveUid, FixedPoint2 CurrentPain);
