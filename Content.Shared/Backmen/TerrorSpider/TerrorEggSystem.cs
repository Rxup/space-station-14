using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Backmen.TerrorSpider;

public sealed class TerrorEggSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    private readonly Dictionary<EntityUid, Entity<EggHolderComponent>> _eggs = [];
    private EntProtoId[] _terrorSpiders = [];
    private float _accumulatedTime;
    private const float HatchInterval = 30f;

    public override void Initialize()
    {
        base.Initialize();

        _terrorSpiders = _prototype.EnumeratePrototypes<EntityPrototype>()
            .Where(p => p.ID.StartsWith("MobTerror") && !p.Abstract)
            .Select(p => new EntProtoId(p.ID))
            .ToArray();

        SubscribeLocalEvent<EggHolderComponent, ComponentInit>(OnEggAdded);
        SubscribeLocalEvent<EggHolderComponent, ComponentShutdown>(OnEggRemoved);
    }

    private void OnEggAdded(Entity<EggHolderComponent> ent, ref ComponentInit args) => _eggs.TryAdd(ent.Owner, ent);

    private void OnEggRemoved(Entity<EggHolderComponent> ent, ref ComponentShutdown args) => _eggs.Remove(ent.Owner);

    public override void Update(float frameTime)
    {
        _accumulatedTime += frameTime;

        if (_accumulatedTime >= HatchInterval)
        {
            _accumulatedTime -= HatchInterval;

            foreach (var egg in _eggs.Values)
            {
                HatchEgg(egg.Owner);
            }
        }
    }

    private void HatchEgg(EntityUid eggUid)
    {
        if (!_terrorSpiders.Any() || Deleted(eggUid))
            return;

        var xform = Transform(eggUid);
        if (!xform.Coordinates.IsValid(EntityManager))
            return;

        var entity = EntityManager.SpawnEntity(_random.Pick(_terrorSpiders), xform.Coordinates);
        RemComp<EggHolderComponent>(eggUid);
    }
}
