using System.Linq;
using System.Numerics;
using Content.Client.Graphics;
using Content.Shared.Backmen.Shipwrecked.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Backmen.Shipwrecked;

/// <summary>
/// Client visualizer for <see cref="HologramComponent"/>: holopad-style scanline post-shader
/// on the existing sprite (does not replace layers like <c>HolopadSystem</c>).
/// </summary>
public sealed partial class HologramVisualizerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HologramComponent, BeforePostShaderRenderEvent>(OnShaderRender);
    }

    private void OnStartup(Entity<HologramComponent> entity, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(entity, out var sprite))
            return;

        Apply(entity, sprite);
    }

    private void OnShaderRender(Entity<HologramComponent> entity, ref BeforePostShaderRenderEvent args)
    {
        if (args.Id != ContentPostShaderIds.Holopad)
            return;

        if (!TryComp<SpriteComponent>(entity, out var sprite))
            return;

        Apply(entity, sprite, args.Shader);
    }

    private void Apply(Entity<HologramComponent> entity, SpriteComponent sprite, ShaderInstance? shader = null)
    {
        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            if (_sprite.TryGetLayer((entity.Owner, sprite), i, out var layer, false) &&
                layer.ShaderPrototype != "DisplacedDraw")
            {
                sprite.LayerSetShader(i, "unshaded");
            }
        }

        float texHeight = 1f;
        if (sprite.AllLayers.Any())
            texHeight = sprite.AllLayers.Max(x => x.PixelSize.Y);

        var hologram = entity.Comp;
        var instance = shader ?? _proto.Index<ShaderPrototype>(hologram.ShaderName).InstanceUnique();
        instance.SetParameter("color1", new Vector3(hologram.Color1.R, hologram.Color1.G, hologram.Color1.B));
        instance.SetParameter("color2", new Vector3(hologram.Color2.R, hologram.Color2.G, hologram.Color2.B));
        instance.SetParameter("alpha", hologram.Alpha);
        instance.SetParameter("intensity", hologram.Intensity);
        instance.SetParameter("texHeight", texHeight);
        instance.SetParameter("t", (float)_timing.CurTime.TotalSeconds * hologram.ScrollRate);

        _sprite.SetPostShader((entity.Owner, sprite), new SpriteComponent.PostShaderArgs(ContentPostShaderIds.Holopad, instance)
        {
            RaiseShaderEvent = true,
            Before = ContentPostShaderIds.BeforeOutlines,
        });
    }
}
