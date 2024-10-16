using Content.Shared.Backmen.FootPrints;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Client.Backmen.FootPrints;

public sealed class FootPrintsVisualizerSystem : VisualizerSystem<FootPrintComponent>
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FootPrintComponent, ComponentInit>(OnInitialized);
        SubscribeLocalEvent<FootPrintComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnInitialized(EntityUid uid, FootPrintComponent comp, ComponentInit args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        sprite.LayerMapReserveBlank(FootPrintVisualLayers.Print);
        UpdateAppearance(uid, comp, sprite);
    }

    private void OnShutdown(EntityUid uid, FootPrintComponent comp, ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite) &&
            sprite.LayerMapTryGet(FootPrintVisualLayers.Print, out var layer))
        {
            sprite.RemoveLayer(layer);
        }
    }

    private void UpdateAppearance(EntityUid uid, FootPrintComponent component, SpriteComponent sprite)
    {
        //meow-meow-meow
        if (!sprite.LayerMapTryGet(FootPrintVisualLayers.Print, out var layer))
            return;

        if (!TryComp<FootPrintsComponent>(component.PrintOwner, out var printsComponent))
            return;

        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        if (_appearance.TryGetData<FootPrintVisuals>(uid, FootPrintVisualState.State, out var printVisuals, appearance))
        {
            var path = new ResPath("/Textures/Backmen/Decals/footprints.rsi");
            switch (printVisuals)
            {
                case FootPrintVisuals.BareFootPrint:
                    sprite.LayerSetState(layer,
                        printsComponent.RightStep
                            ? new RSI.StateId(printsComponent.RightBarePrint)
                            : new RSI.StateId(printsComponent.LeftBarePrint),
                        path);
                    break;
                case FootPrintVisuals.ShoesPrint:
                    sprite.LayerSetState(layer, new RSI.StateId(printsComponent.ShoesPrint), path);
                    break;
                case FootPrintVisuals.SuitPrint:
                    sprite.LayerSetState(layer, new RSI.StateId(printsComponent.SuitPrint), path);
                    break;
                case FootPrintVisuals.Dragging:
                    sprite.LayerSetState(layer, new RSI.StateId(_random.Pick(printsComponent.DraggingPrint)), path);
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown {printVisuals} parameter.");
            }
        }

        if (!_appearance.TryGetData<Color>(uid, FootPrintVisualState.Color, out var printColor, appearance))
            return;

        sprite.LayerSetColor(layer, printColor);
    }

    protected override void OnAppearanceChange (EntityUid uid, FootPrintComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        UpdateAppearance(uid, component, sprite);
    }
}
