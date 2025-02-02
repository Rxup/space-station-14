using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Backmen.Surgery.Pain.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NerveSystemComponent : Component
{
    /// <summary>
    /// Pain.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 Pain = 0f;

    /// <summary>
    /// How much Pain can this nerve system hold.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 PainCap = 100f;

    // Don't change, OR I will break your knees, filled up upon initialization.
    public Dictionary<EntityUid, NerveComponent> Nerves = new();

    // Don't add manually!! Use built-in functions.
    public Dictionary<string, PainMultiplier> Multipliers = new();
    public Dictionary<EntityUid, PainModifier> Modifiers = new();

    public Dictionary<EntityUid, AudioComponent> PlayedPainSounds = new();

    [DataField("lastThreshold"), ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 LastPainThreshold = 0;

    [DataField("thresholdUpdate")]
    public TimeSpan ThresholdUpdateTime = TimeSpan.FromSeconds(2);

    [DataField("accumulated", customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan UpdateTime = TimeSpan.Zero;

    [DataField("painShockStun")]
    public TimeSpan PainShockStunTime = TimeSpan.FromSeconds(12);

    [DataField("passoutTime")]
    public TimeSpan ForcePassoutTime = TimeSpan.FromSeconds(7);

    [DataField]
    public Dictionary<Sex, SoundSpecifier> PainScreams = new()
    {
        {
            Sex.Male, new SoundCollectionSpecifier("PainScreamsShortMale")
            {
                Params = AudioParams.Default.WithVariation(0.04f),
            }
        },
        {
            Sex.Female, new SoundCollectionSpecifier("PainScreamsShortMale") // TODO: Female screams. Temporary for now.
            {
                Params = AudioParams.Default.WithVariation(0.04f),
            }
        },
        {
            Sex.Unsexed, new SoundCollectionSpecifier("PainScreamsShortMale") // yeah
            {
                Params = AudioParams.Default.WithVariation(0.2f),
            }
        },
    };

    [DataField]
    public Dictionary<Sex, SoundSpecifier> PainShockScreams = new()
    {
        {
            Sex.Male, new SoundCollectionSpecifier("PainShockScreamsMale")
            {
                Params = AudioParams.Default.WithVariation(0.05f),
            }
        },
        {
            Sex.Female, new SoundCollectionSpecifier("PainShockScreamsMale") // TODO: Female screams. Temporary for now.
            {
                Params = AudioParams.Default.WithVariation(0.05f),
            }
        },
        {
            Sex.Unsexed, new SoundCollectionSpecifier("PainShockScreamsMale") // yeah
            {
                Params = AudioParams.Default.WithVariation(0.2f),
            }
        },
    };

    [DataField]
    public Dictionary<Sex, SoundSpecifier> CritWhimpers = new()
    {
        {
            Sex.Male, new SoundCollectionSpecifier("CritWhimpersMale")
            {
                Params = AudioParams.Default.WithVolume(-0.7f),
            }
        },
        {
            Sex.Female, new SoundCollectionSpecifier("CritWhimpersMale") // TODO: Female screams. Temporary for now.
            {
                Params = AudioParams.Default.WithVolume(-0.7f),
            }
        },
        {
            Sex.Unsexed, new SoundCollectionSpecifier("CritWhimpersMale") // yeah
            {
                Params = AudioParams.Default.WithVolume(-0.7f),
            }
        },
    };

    [DataField]
    public Dictionary<Sex, SoundSpecifier> PainShockWhimpers = new()
    {
        {
            Sex.Male, new SoundCollectionSpecifier("PainShockWhimpersMale")
            {
                Params = AudioParams.Default.WithVolume(-0.7f),
            }
        },
        {
            Sex.Female, new SoundCollectionSpecifier("PainShockWhimpersMale") // TODO: Female screams. Temporary for now.
            {
                Params = AudioParams.Default.WithVolume(-0.7f),
            }
        },
        {
            Sex.Unsexed, new SoundCollectionSpecifier("PainShockWhimpersMale") // yeah
            {
                Params = AudioParams.Default.WithVolume(-0.7f),
            }
        },
    };

    [DataField("reflexThresholds"), ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<PainThresholdTypes, FixedPoint2> PainThresholds = new()
    {
        { PainThresholdTypes.PainFlinch, 6 },
        { PainThresholdTypes.PainShock, 27 },
        { PainThresholdTypes.PainPassout, 50 },
        // usually appears after an explosion. or some ultra big damage output thing, you might survive, and most importantly, you will fall down in pain.
        // :troll:
    };
}
