using Content.Shared.Backmen.Blob;
using Content.Shared.Backmen.Blob.Components;
using Content.Shared.Backmen.Eye.NightVision.Components;
using Content.Shared.CCVar;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Mobs.Components;
using Robust.Client.Console;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;

namespace Content.Client.Backmen;

public sealed class LightHandleSystem : EntitySystem
{
    [Dependency] private readonly ILightManager _light = default!;
    [Dependency] private readonly IClientConGroupController _conGroup = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        var screenShakeIntensity = _configurationManager.GetCVar(CCVars.ScreenShakeIntensity);
        if (screenShakeIntensity < 0.35)
        {
            _configurationManager.SetCVar(CCVars.ScreenShakeIntensity, 0.35f, true);
            _configurationManager.SaveToFile();
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_light is { Enabled: true, DrawShadows: true, DrawHardFov: true, DrawLighting: true })
        {
            return;
        }

        if (_conGroup.CanAdminPlace())
        {
            return;
        }

        var plr = _playerManager.LocalSession?.AttachedEntity;
        if (plr == null)
            return;
        if (!HasComp<MobStateComponent>(plr))
            return;
        if (HasComp<BlobObserverComponent>(plr))
            return;
        if (TryComp<BlindableComponent>(plr, out var blindableComponent) && blindableComponent.LightSetup)
            return;
        if (TryComp<NightVisionComponent>(plr, out var nightVisionComponent) && nightVisionComponent.IsNightVision)
            return;

        _light.Enabled = true;
        _light.DrawShadows = true;
        _light.DrawHardFov = true;
        _light.DrawLighting = true;
    }
}
