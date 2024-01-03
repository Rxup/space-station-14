using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;

namespace Content.Server.Backmen.Chapel;

[RegisterComponent]
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
    public string RewardPool = "PsionicArtifactPool";

    [DataField("rewardPoolChance")]
    public float RewardPoolChance = 0.1f;

    [DataField("rewardPoolChanceBibleUser")]
    public float RewardPoolChanceBibleUser = 0.5f;

    [DataField("bluespaceRewardMin")]
    public int BluespaceRewardMin = 1;

    [DataField("bluespaceRewardMax")]
    public int BlueSpaceRewardMax = 4;

    [DataField("glimmerReductionMin")]
    public int GlimmerReductionMin = 30;

    [DataField("glimmerReductionMax")]
    public int GlimmerReductionMax = 60;

    [DataField("trapPrototype")]
    public string TrapPrototype = "CrystalSoul";

    /// <summary>
    ///     Antiexploit.
    /// </summary>
    public TimeSpan? StunTime = null;

    [DataField("stateCD")]
    public TimeSpan StunCD = TimeSpan.FromSeconds(30);
}
