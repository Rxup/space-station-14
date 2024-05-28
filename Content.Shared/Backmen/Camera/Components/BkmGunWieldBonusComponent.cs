using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Camera.Components;


[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BkmGunWieldBonusComponent : Component
{
    /// <summary>
    ///     The maximum magnitude of the kick applied to the camera at any point.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("kickMagnitudeMax"), AutoNetworkedField]
    public float KickMagnitudeMax = 6f;

    [ViewVariables(VVAccess.ReadWrite), DataField("cameraRecoilScalar"), AutoNetworkedField]
    public float CameraRecoilScalar = 1f;
}
