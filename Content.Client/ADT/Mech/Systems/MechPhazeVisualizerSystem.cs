using Robust.Client.GameObjects;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.ADT.Mech.Components;

namespace Content.Client.ADT.Mech;

public sealed class MechPhazeVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechPhazeComponent, AppearanceChangeEvent>(OnAppearanceChange);

    }

    private void OnAppearanceChange(EntityUid uid, MechPhazeComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (_appearance.TryGetData<bool>(uid, MechPhazingVisuals.Phazing, out var phaze, args.Component))
        {
            if (phaze)
                args.Sprite.LayerSetState(0, component.PhazingState);
            else
                args.Sprite.LayerSetState(0, component.State);
        }
    }
}
