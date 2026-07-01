using Content.Server._Mono.Projectiles.TargetSeeking;
using Robust.Shared.Audio;

namespace Content.Server._Mono.TargetSeekingAlert;

[RegisterComponent]
public sealed partial class TargetSeekerAlertComponent : Component
{
    [DataField]
    public SoundSpecifier? TargetGainSound;

    [DataField]
    public List<TargetSeekerAlertSetting> DistanceAlertSettings = new();

    public EntityUid? Audio;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float? ActiveAlertSoundKey;
}

[DataDefinition]
public partial record struct TargetSeekerAlertSetting()
{
    [DataField]
    public float MaximumDistance;

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_Mono/Effects/target_seeker_beep.ogg");
}

[RegisterComponent]
public sealed partial class TargetSeekerAlertGridComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public HashSet<Entity<TargetSeekingComponent, TransformComponent>> CurrentSeekers = new();

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public List<EntityUid> Alerters = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public HashSet<Entity<TargetSeekerAlertComponent>> ActiveAlerters = new();
}
