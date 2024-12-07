using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery.Wounds.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WoundComponent : Component
{
    /// <summary>
    /// 'Parent' of wound. Basically the entity to which the wound was applied.
    /// </summary>
    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid Parent;

    /// <summary>
    /// Actually, severity of the wound. The more the worse.
    /// Directly depends on <see cref="WoundSeverity"/>
    /// </summary>
    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 WoundSeverityPoint;

    /// <summary>
    /// Multipliers of severity applied to this wound.
    /// </summary>
    public Dictionary<(EntityUid, WoundComponent), WoundSeverityMultiplier> SeverityMultipliers = new();

    /// <summary>
    /// Base healing rate for wound. Cool!
    /// </summary>
    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 BaseHealingRate;

    /// <summary>
    /// Multipliers applied to healing rate.
    /// </summary>
    public Dictionary<(EntityUid, WoundComponent), HealingMultiplier> HealingMultipliers = new();

    /// <summary>
    /// Wound type. External/Internal basically.
    /// </summary>
    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public WoundType WoundType;

    /// <summary>
    /// Damage group of this wound.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public string DamageGroup;

    /// <summary>
    /// Scar wound prototype, what will be spawned upon healing this wound.
    /// If null - no scar wound will be spawned.
    /// </summary>
    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public string? ScarWound;

    /// <summary>
    /// Well, name speaks for this.
    /// </summary>
    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public bool IsScar;

    /// <summary>
    /// Wound severity. Has six severities: Healed/Minor/Moderate/Severe/Critical and Loss.
    /// Directly depends on <see cref="WoundSeverityPoint"/>
    /// </summary>
    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public WoundSeverity WoundSeverity;

    /// <summary>
    /// When wound is visible. Always/HandScanner/AdvancedScanner.
    /// </summary>
    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public WoundVisibility WoundVisibility;

    /// <summary>
    /// "Can be healed".
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool CanBeHealed = true;

    /// <summary>
    /// Should this bleed?
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool CanBleed = true;

    /// <summary>
    /// Frame time, accumulated by this wound.
    /// </summary>
    public float AccumulatedFrameTime; //weoweoweo
}
