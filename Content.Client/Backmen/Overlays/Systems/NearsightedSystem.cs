using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Network;
using Content.Shared.Tag;
using Content.Shared.Backmen.Traits;

namespace Content.Client.Backmen.Overlays;
public sealed class NearsightedSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;

    private NearsightedOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new Overlays.NearsightedOverlay();
    }

    [ValidatePrototypeId<TagPrototype>]
    private const string TagName = "GlassesNearsight";

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var q = EntityQueryEnumerator<NearsightedComponent, TagComponent>();

        while (q.MoveNext(out var nearsight, out var tagComponent))
        {
            UpdateShader(nearsight, _tagSystem.HasTag(tagComponent, TagName));
        }
    }


    private void UpdateShader(NearsightedComponent component, bool booLean)
    {
        while (_overlayMan.HasOverlay<NearsightedOverlay>())
        {
            _overlayMan.RemoveOverlay(_overlay);
        }

        component.Glasses = booLean;
        _overlayMan.AddOverlay(_overlay);
    }
}
