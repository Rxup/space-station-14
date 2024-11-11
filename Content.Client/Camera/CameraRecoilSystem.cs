using System.Numerics;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Client.Camera;

public sealed class CameraRecoilSystem : SharedCameraRecoilSystem
{
    [Dependency] private readonly IConfigurationManager _configManager = default!;

    private float _intensity;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CameraKickEvent>(OnCameraKick);

        Subs.CVar(_configManager, CCVars.ScreenShakeIntensity, OnCvarChanged, true);
    }

    private void OnCvarChanged(float value)
    {
        _intensity = value;
    }

    private void OnCameraKick(CameraKickEvent ev)
    {
        KickCamera(GetEntity(ev.NetEntity), ev.Recoil);
    }

    public override void KickCamera(
        EntityUid uid,
        Vector2 recoil,
        CameraRecoilComponent? component = null,
        float? kickMagnitudeMax = null // backmen: KickMagnitudeMax
        )
    {
        if (_intensity == 0)
            return;

        if (!Resolve(uid, ref component, false))
            return;

        recoil *= _intensity;

        kickMagnitudeMax = kickMagnitudeMax ?? KickMagnitudeMax; // backmen: KickMagnitudeMax

        // Use really bad math to "dampen" kicks when we're already kicked.
        var existing = component.CurrentKick.Length();
        var dampen = existing / kickMagnitudeMax.Value; // backmen: KickMagnitudeMax
        component.CurrentKick += recoil * (1 - dampen);

        if (component.CurrentKick.Length() > kickMagnitudeMax.Value) // backmen: KickMagnitudeMax
            component.CurrentKick = component.CurrentKick.Normalized() * kickMagnitudeMax.Value; // backmen: KickMagnitudeMax

        component.LastKickTime = 0;
    }
}
