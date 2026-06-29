using Content.Shared.Backmen.Silicon.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Backmen.Overlays.Shaders;

public sealed partial class StaticOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> SeeingStaticShader = "SeeingStatic";

    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly StatusEffectsSystem _statusEffects;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;
    private readonly ShaderInstance _staticShader;

    public float MixAmount;

    public StaticOverlay()
    {
        IoCManager.InjectDependencies(this);
        _statusEffects = _entityManager.System<StatusEffectsSystem>();
        _staticShader = _prototypeManager.Index(SeeingStaticShader).InstanceUnique();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var playerEntity = _playerManager.LocalEntity;

        if (playerEntity == null)
            return;

        if (!_statusEffects.TryGetMaxTime<SeeingStaticComponent>(playerEntity.Value, out var status))
        {
            MixAmount = 0;
            return;
        }

        if (!_entityManager.TryGetComponent(status.EffectEnt, out SeeingStaticComponent? staticComp))
            return;

        if (status.EndEffectTime == null)
        {
            MixAmount = Math.Clamp(staticComp.Multiplier, 0, 1);
            return;
        }

        if (!_entityManager.TryGetComponent(status.EffectEnt, out StatusEffectComponent? effectComp))
            return;

        var fullDuration = (float) effectComp.Duration.TotalSeconds;
        if (fullDuration <= 0)
        {
            MixAmount = 0;
            return;
        }

        var timeLeft = (float) (status.EndEffectTime.Value - _timing.CurTime).TotalSeconds;
        MixAmount = Math.Clamp(timeLeft / fullDuration * staticComp.Multiplier, 0, 1);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eyeComp))
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
