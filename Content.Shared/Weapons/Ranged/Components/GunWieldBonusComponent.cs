using Content.Shared.Wieldable;
using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Ranged.Components;

/// <summary>
/// Applies an accuracy bonus upon wielding.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(WieldableSystem))]
public sealed partial class GunWieldBonusComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("minAngle"), AutoNetworkedField]
    public Angle MinAngle = Angle.FromDegrees(-43);

    /// <summary>
    /// Angle bonus applied upon being wielded.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("maxAngle"), AutoNetworkedField]
    public Angle MaxAngle = Angle.FromDegrees(-43);
    ///CameraRecoilScalar GunWieldBonus start
    /// <summary>
    /// A scalar value applied to the vector governing camera recoil.
    /// If 0, there will be no camera recoil.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("cameraRecoilScalar"), AutoNetworkedField]
    public float CameraRecoilScalar = 0f;
    ///CameraRecoilScalar GunWieldBonus end

    /// <summary>
    /// Recoil bonuses applied upon being wielded.
    /// Higher angle decay bonus, quicker recovery.
    /// Lower angle increase bonus (negative numbers), slower buildup.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Angle AngleDecay = Angle.FromDegrees(0);

	/// <summary>
    /// Recoil bonuses applied upon being wielded.
    /// Higher angle decay bonus, quicker recovery.
    /// Lower angle increase bonus (negative numbers), slower buildup.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Angle AngleIncrease = Angle.FromDegrees(0);

    [DataField]
    public LocId? WieldBonusExamineMessage = "gunwieldbonus-component-examine";
}
