using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery.Pain.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NerveSystemComponent : Component
{
    /// <summary>
    /// Pain.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 Pain = 0f;

    /// <summary>
    /// How much Pain can this nerve system hold.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 PainCap = 250f;

    /// <summary>
    /// How much of typical wound pain can this nerve system hold?
    /// Also is the point at which entity will enter pain crit.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 SoftPainCap = 105f;

    /// <summary>
    /// The entity of the body part, in which the nerve system is stored in
    /// </summary>
    public EntityUid RootNerve;

    /// <summary>
    /// Is the entity forced into pain crit?
    /// </summary>
    public bool ForcePainCrit;

    /// <summary>
    /// When will the pain end?
    /// </summary>
    public TimeSpan ForcePainCritEnd;

    // Don't change, OR I will break your knees, filled up upon initialization.
    public Dictionary<EntityUid, NerveComponent> Nerves = new();

    // Don't add manually!! Use built-in functions.
    public Dictionary<string, PainMultiplier> Multipliers = new();
    public Dictionary<(EntityUid, string), PainModifier> Modifiers = new();

    public Dictionary<EntityUid, AudioComponent> PlayedPainSounds = new();
    public Dictionary<SoundSpecifier, (AudioParams?, TimeSpan)> PainSoundsToPlay = new();

    [DataField("lastThreshold"), ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 LastPainThreshold = 0;

    [ViewVariables(VVAccess.ReadOnly)]
    public PainThresholdTypes LastThresholdType = PainThresholdTypes.None;

    [DataField("thresholdUpdate")]
    public TimeSpan ThresholdUpdateTime = TimeSpan.FromSeconds(1.6f);

    [DataField("reactionTime")]
    public TimeSpan PainReactionTime = TimeSpan.FromSeconds(0.07f);

    [DataField("adrenalineTime")]
    public TimeSpan PainShockAdrenalineTime = TimeSpan.FromSeconds(40f);

    [DataField]
    public TimeSpan PainScreamsIntervalMin = TimeSpan.FromSeconds(8f);

    [DataField]
    public TimeSpan PainScreamsIntervalMax = TimeSpan.FromSeconds(16f);

    public TimeSpan UpdateTime;
    public TimeSpan ReactionUpdateTime;
    public TimeSpan NextPainScream;

    [DataField("painShockStun")]
    public TimeSpan PainShockCritDuration = TimeSpan.FromSeconds(7f);

    [DataField("organDamageStun")]
    public TimeSpan OrganDamageStunTime = TimeSpan.FromSeconds(12f);

    #region Sounds

    [DataField]
    public SoundSpecifier PainRattles = new SoundCollectionSpecifier("PainRattles");

    [DataField]
    public Dictionary<Sex, SoundSpecifier> PainGrunts = new()
    {
        {
            Sex.Male, new SoundCollectionSpecifier("PainGruntsMale")
            {
                Params = AudioParams.Default.WithVariation(0.07f).WithVolume(-7f),
            }
        },
        {
            Sex.Female, new SoundCollectionSpecifier("PainGruntsFemale")
            {
                Params = AudioParams.Default.WithVariation(0.04f).WithVolume(-4f),
            }
        },
        {
            Sex.Unsexed, new SoundCollectionSpecifier("PainGruntsMale") // yeah
            {
                Params = AudioParams.Default.WithVariation(0.2f).WithVolume(-7f),
            }
        },
    };

    [DataField]
    public Dictionary<Sex, SoundSpecifier> PainScreams = new()
    {
        {
            Sex.Male, new SoundCollectionSpecifier("PainScreamsShortMale")
            {
                Params = AudioParams.Default.WithVariation(0.07f),
            }
        },
        {
            Sex.Female, new SoundCollectionSpecifier("PainScreamsShortFemale")
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
    public Dictionary<Sex, SoundSpecifier> AgonyScreams = new()
    {
        {
            Sex.Male, new SoundCollectionSpecifier("AgonyScreamsMale")
            {
                Params = AudioParams.Default.WithVariation(0.09f).WithVolume(7f),
            }
        },
        {
            Sex.Female, new SoundCollectionSpecifier("AgonyScreamsFemale")
            {
                Params = AudioParams.Default.WithVariation(0.09f).WithVolume(4f),
            }
        },
        {
            Sex.Unsexed, new SoundCollectionSpecifier("AgonyScreamsMale") // yeah
            {
                Params = AudioParams.Default.WithVariation(0.2f).WithVolume(7f),
            }
        },
    };

    [DataField]
    public Dictionary<Sex, SoundSpecifier> PainShockScreams = new()
    {
        {
            Sex.Male, new SoundCollectionSpecifier("PainShockScreamsMale")
            {
                Params = AudioParams.Default.WithVariation(0.09f),
            }
        },
        {
            Sex.Female, new SoundCollectionSpecifier("PainShockScreamsFemale")
            {
                Params = AudioParams.Default.WithVariation(0.05f).WithVolume(-4f),
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
            Sex.Male, new SoundCollectionSpecifier("WhimpersMale")
            {
                Params = AudioParams.Default.WithVolume(-14f),
            }
        },
        {
            Sex.Female, new SoundCollectionSpecifier("WhimpersFemale")
            {
                Params = AudioParams.Default.WithVolume(-14f),
            }
        },
        {
            Sex.Unsexed, new SoundCollectionSpecifier("WhimpersMale") // yeah
            {
                Params = AudioParams.Default.WithVolume(-14f),
            }
        },
    };

    [DataField]
    public Dictionary<Sex, SoundSpecifier> PainedWhimpers = new()
    {
        {
            Sex.Male, new SoundCollectionSpecifier("PainedWhimpersMale")
            {
                Params = AudioParams.Default.WithVolume(-7f),
            }
        },
        {
            Sex.Female, new SoundCollectionSpecifier("PainedWhimpersFemale")
            {
                Params = AudioParams.Default.WithVolume(-7f),
            }
        },
        {
            Sex.Unsexed, new SoundCollectionSpecifier("PainedWhimpersMale") // yeah
            {
                Params = AudioParams.Default.WithVolume(-7f),
            }
        },
    };

    [DataField]
    public Dictionary<Sex, SoundSpecifier> OrganDestructionReflexSounds = new()
    {
        {
            Sex.Male, new SoundCollectionSpecifier("OrganDamagePainedMale")
            {
                Params = AudioParams.Default.WithVariation(0.07f).WithVolume(-2f),
            }
       },
       {
            Sex.Female, new SoundCollectionSpecifier("OrganDamagePainedFemale")
            {
                Params = AudioParams.Default.WithVariation(0.04f),
            }
        },
        {
            Sex.Unsexed, new SoundCollectionSpecifier("OrganDamagePainedMale")
            {
                Params = AudioParams.Default.WithVariation(0.2f).WithVolume(-7f),
            }
        },
    };

    [DataField]
    public Dictionary<Sex, SoundSpecifier> OrganDamageWhimpersSounds = new()
    {
        {
            Sex.Male, new SoundCollectionSpecifier("OrganDamageWhimpersMale")
            {
                Params = AudioParams.Default.WithVolume(-7f),
            }
        },
        {
            Sex.Female, new SoundCollectionSpecifier("OrganDamageWhimpersFemale")
            {
                Params = AudioParams.Default.WithVolume(-7f),
            }
        },
        {
            Sex.Unsexed, new SoundCollectionSpecifier("OrganDamageWhimpersMale")
            {
                Params = AudioParams.Default.WithVolume(-7f),
            }
        },
    };

    [DataField]
    public Dictionary<Sex, SoundSpecifier> ExtremePainSounds = new()
    {
        {
            Sex.Male, new SoundCollectionSpecifier("ExtremePainMale")
            {
                Params = AudioParams.Default.WithVariation(0.07f).WithVolume(20f),
            }
        },
        {
            Sex.Female, new SoundCollectionSpecifier("ExtremePainFemale")
            {
                Params = AudioParams.Default.WithVariation(0.04f).WithVolume(20f),
            }
        },
        {
            Sex.Unsexed, new SoundCollectionSpecifier("ExtremePainMale") // yeah
            {
                Params = AudioParams.Default.WithVariation(0.2f).WithVolume(20f),
            }
        },
    };

    #endregion

    [DataField("reflexThresholds"), ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<PainThresholdTypes, FixedPoint2> PainThresholds = new()
    {
        { PainThresholdTypes.PainGrunt, 1.8 },
        { PainThresholdTypes.PainFlinch, 7.2 },
        { PainThresholdTypes.Agony, 27 },
        // Just having 'PainFlinch' is lame, people scream for a few seconds before passing out / getting pain shocked, so I added agony.
        // A lot of screams (individual pain screams poll), for the funnies.
        { PainThresholdTypes.PainShock, 52 },
        // usually appears after an explosion. or some ultra big damage output thing, you might survive, and most importantly, you will fall down in pain.
        // :troll:
        { PainThresholdTypes.PainShockAndAgony, 80 },
    };
}
