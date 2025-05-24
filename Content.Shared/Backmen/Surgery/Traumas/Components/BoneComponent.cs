using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery.Traumas.Components;

[RegisterComponent, AutoGenerateComponentState, NetworkedComponent]
public sealed partial class BoneComponent : Component
{
    [AutoNetworkedField, ViewVariables]
    public EntityUid? BoneWoundable;

    [DataField, AutoNetworkedField, ViewVariables]
    public FixedPoint2 IntegrityCap = 60f;

    [DataField, AutoNetworkedField, ViewVariables]
    public FixedPoint2 BoneIntegrity = 60f;

    [DataField, AutoNetworkedField, ViewVariables]
    public Dictionary<BoneSeverity, FixedPoint2> BoneThresholds = new()
    {
        { BoneSeverity.Normal, 60 },
        { BoneSeverity.Damaged, 36 },
        { BoneSeverity.Broken, 0 },
    };

    [DataField, AutoNetworkedField, ViewVariables]
    public FixedPoint2 BoneRegenerationRate = 0.1f;

    [AutoNetworkedField, ViewVariables]
    public BoneSeverity BoneSeverity = BoneSeverity.Normal;

    [DataField]
    public SoundSpecifier BoneBreakSound = new SoundCollectionSpecifier("BoneGone");
}
