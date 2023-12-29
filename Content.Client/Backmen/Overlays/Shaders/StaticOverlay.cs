using Content.Client.Backmen.Silicon.Systems;
using Content.Shared.Backmen.Silicon.Components;
using Content.Shared.StatusEffect;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Backmen.Overlays.Shaders;

public sealed class StaticOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;
    private readonly ShaderInstance _staticShader;

    private (TimeSpan, TimeSpan)? _time;
    private float? _fullTimeLeft;
    private float? _curTimeLeft;

    public float MixAmount = 0;

    public StaticOverlay()
    {
        IoCManager.InjectDependencies(this);
        _staticShader = _prototypeManager.Index<ShaderPrototype>("SeeingStatic").InstanceUnique();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var playerEntity = _playerManager.LocalPlayer?.ControlledEntity;

        if (playerEntity == null)
            return;

        if (!_entityManager.TryGetComponent<SeeingStaticComponent>(playerEntity, out var staticComp)
            || !_entityManager.TryGetComponent<StatusEffectsComponent>(playerEntity, out var statusComp))
            return;

        var status = _entityManager.EntitySysManager.GetEntitySystem<StatusEffectsSystem>();

        if (playerEntity == null || statusComp == null)
            return;

        if (!status.TryGetTime(playerEntity.Value, SeeingStaticSystem.StaticKey, out var timeTemp, statusComp))
            return;

        if (_time != timeTemp) // Resets the shader if the times change. This should factor in wheather it's a reset, or a increase, but I have a lot of cough syrup in me, so TODO.
        {
            _time = timeTemp;
            _fullTimeLeft = null;
            _curTimeLeft = null;
        }

        _fullTimeLeft ??= (float) (timeTemp.Value.Item2 - timeTemp.Value.Item1).TotalSeconds;
        _curTimeLeft ??= _fullTimeLeft;

        _curTimeLeft -= args.DeltaSeconds;

        MixAmount = Math.Clamp(_curTimeLeft.Value / _fullTimeLeft.Value * staticComp.Multiplier, 0, 1);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalPlayer?.ControlledEntity, out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        return MixAmount > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        _staticShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _staticShader.SetParameter("mixAmount", MixAmount);
        handle.UseShader(_staticShader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
