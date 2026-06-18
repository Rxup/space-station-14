using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.VentCrawler;

public abstract class SharedVentCrawlerSystem : EntitySystem
{
    public static readonly EntProtoId GasPipeBrokenPrototype = "GasPipeBroken";
    public static readonly ProtoId<TagPrototype> GasVentTag = "GasVent";

    [Dependency] private readonly TagSystem _tag = default!;

    public bool IsGasVent(EntityUid uid)
    {
        return _tag.HasTag(uid, GasVentTag);
    }
}
