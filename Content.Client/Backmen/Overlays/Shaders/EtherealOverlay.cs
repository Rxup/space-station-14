using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client.Backmen.Overlays.Shaders;

public sealed class EtherealOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    private readonly ShaderInstance _shader;

    [ValidatePrototypeId<ShaderPrototype>] private const string Ethereal = "Ethereal";

    public EtherealOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototype.Index<ShaderPrototype>(Ethereal).InstanceUnique();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null) return;
        if (
            _player.LocalSession?.AttachedEntity
            is not { Valid: true } player
        )
            return;

        _shader?.SetParameter("SCREEN_TEXTURE", ScreenTexture);

        var worldHandle = args.WorldHandle;
        var viewport = args.WorldBounds;
        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(_shader);
        worldHandle.DrawRect(viewport, Color.White);
        worldHandle.UseShader(null);
    }
}
