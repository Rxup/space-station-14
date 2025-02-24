using Content.Shared.DoAfter;
using Content.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Chapel.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SacrificialAltarComponent : Component
{
    [DataField("doAfter")]
    public DoAfterId? DoAfter;

    [DataField("sacrificeTime")]
    public TimeSpan SacrificeTime = TimeSpan.FromSeconds(8.35);

    [DataField("sacrificeSound")]
    public SoundSpecifier SacrificeSoundPath = new SoundPathSpecifier("/Audio/Effects/clang2.ogg");

    public Entity<AudioComponent>? SacrificeStingStream;

    [DataField("rewardPool")]
    public ProtoId<WeightedRandomPrototype> RewardPool = "PsionicArtifactPool";

    [DataField("rewardPoolChance")]
    public float RewardPoolChance = 0.1f;

    [DataField("rewardPoolChanceBibleUser")]
    public float RewardPoolChanceBibleUser = 0.5f;

    [DataField("bluespaceRewardMin")]
    public int BluespaceRewardMin = 4;

    [DataField("bluespaceRewardMax")]
    public int BlueSpaceRewardMax = 8;

    [DataField("glimmerReductionMin")]
    public int GlimmerReductionMin = 300;

    [DataField("glimmerReductionMax")]
    public int GlimmerReductionMax = 600;

    [DataField("trapPrototype")]
    public EntProtoId TrapPrototype = "CrystalSoul";

    /// <summary>
    ///     Antiexploit.
    /// </summary>
    public TimeSpan? StunTime = null;

    [DataField("stateCD")]
    public TimeSpan StunCD = TimeSpan.FromSeconds(30);
}
