using Content.Server.Lathe.Components;
using Content.Shared.Backmen.Lathe;
using Content.Shared.Lathe;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Lathe;

public sealed class BkmBiofabricatorSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    private EntityQuery<LatheProducingComponent> _producingQuery;

    public override void Initialize()
    {
        base.Initialize();

        _producingQuery = GetEntityQuery<LatheProducingComponent>();

        SubscribeLocalEvent<BkmBiofabricatorComponent, LatheStartPrintingEvent>(OnStartPrinting);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BkmBiofabricatorComponent, LatheComponent>();
        while (query.MoveNext(out var uid, out var bkm, out var lathe))
        {
            if (lathe.CurrentRecipe == null
                || !_producingQuery.TryGetComponent(uid, out var producing))
            {
                if (bkm.IsProducing)
                {
                    bkm.IsProducing = false;
                    bkm.ProductionDuration = TimeSpan.Zero;
                    Dirty(uid, bkm);
                }

                continue;
            }

            var changed = !bkm.IsProducing
                          || bkm.ProductionStart != producing.StartTime
                          || bkm.ProductionDuration != producing.ProductionLength;

            if (!changed)
                continue;

            bkm.IsProducing = true;
            bkm.ProductionStart = producing.StartTime;
            bkm.ProductionDuration = producing.ProductionLength;
            Dirty(uid, bkm);
        }
    }

    private void OnStartPrinting(Entity<BkmBiofabricatorComponent> ent, ref LatheStartPrintingEvent args)
    {
        if (!_producingQuery.TryGetComponent(ent, out var producing))
            return;

        ent.Comp.IsProducing = true;
        ent.Comp.ProductionStart = producing.StartTime;
        ent.Comp.ProductionDuration = producing.ProductionLength;
        Dirty(ent, ent.Comp);
    }
}
