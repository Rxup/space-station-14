using Content.Client.Lathe;
using Content.Shared.Backmen.Lathe;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.Backmen.Lathe;

public sealed class BkmBiofabricatorSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    /// <summary>
    /// Duration of the limbgrower_fill RSI animation (9 frames at 0.2s).
    /// </summary>
    private const float FillAnimationLength = 1.8f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BkmBiofabricatorComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var bkm, out var sprite))
        {
            if (!_sprite.LayerMapTryGet((uid, sprite), BkmBiofabricatorVisualLayers.Fill, out var fillLayer, false))
                continue;

            var progress = GetProductionProgress(bkm);
            if (!bkm.IsProducing || progress <= 0f)
            {
                _sprite.LayerSetVisible((uid, sprite), fillLayer, false);
                continue;
            }

            _sprite.LayerSetVisible((uid, sprite), fillLayer, true);
            _sprite.LayerSetAutoAnimated((uid, sprite), fillLayer, false);
            _sprite.LayerSetAnimationTime((uid, sprite), fillLayer, progress * FillAnimationLength);
        }
    }

    public float GetProductionProgress(BkmBiofabricatorComponent component)
    {
        if (!component.IsProducing || component.ProductionDuration <= TimeSpan.Zero)
            return 0f;

        var elapsed = _timing.CurTime - component.ProductionStart;
        return Math.Clamp((float) (elapsed / component.ProductionDuration), 0f, 1f);
    }
}

public enum BkmBiofabricatorVisualLayers : byte
{
    Fill,
}
