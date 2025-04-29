using Content.Shared.Backmen.Silicons.Borgs;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Client.Backmen.Silicon;

public sealed partial class BorgSwitchableSubtypeSystem : SharedBorgSwitchableSubtypeSystem
{
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgSwitchableSubtypeComponent, BorgSubtypeChangedEvent>(OnSubtypeChanged);
    }

    private void OnSubtypeChanged(Entity<BorgSwitchableSubtypeComponent> ent, ref BorgSubtypeChangedEvent args)
    {
        SetAppearanceFromSubtype(ent, args.Subtype);
    }

    protected override void SetAppearanceFromSubtype(
        Entity<BorgSwitchableSubtypeComponent> ent,
        ProtoId<BorgSubtypePrototype> subtype)
    {

        if (!Prototypes.TryIndex(subtype, out var subtypePrototype))
            return;

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var rsiPath = SpriteSpecifierSerializer.TextureRoot / subtypePrototype.SpritePath;

        if(_resourceCache.TryGetResource<RSIResource>(rsiPath, out var resource))
            sprite.BaseRSI = resource.RSI;
    }
}
