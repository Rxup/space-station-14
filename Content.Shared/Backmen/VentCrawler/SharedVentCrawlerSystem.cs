using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.VentCrawler;

public abstract partial class SharedVentCrawlerSystem : EntitySystem
{
    public static readonly EntProtoId GasPipeBrokenPrototype = "GasPipeBroken";
    public static readonly ProtoId<TagPrototype> GasVentTag = "GasVent";

    [Dependency] private TagSystem _tag = default!;

    public bool IsGasVent(EntityUid uid)
    {
        return _tag.HasTag(uid, GasVentTag);
    }
}
