using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Backmen.TerrorSpider;

public sealed class TerrorEggSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    private readonly Dictionary<EntityUid, Entity<EggHolderComponent>> _eggs = [];
    private readonly EntProtoId[] _terrorSpiders = ["MobTerrorGray", "MobTerrorGreen", "MobTerrorRed"];
    private DamageTypePrototype? _blunt;
    private DamageSpecifier? _damage;
    private float _accumulatedTime;
    private const float HatchInterval = 5f;

    public override void Initialize()
    {
        SubscribeLocalEvent<EggHolderComponent, ComponentInit>(OnEggAdded);
        SubscribeLocalEvent<EggHolderComponent, ComponentShutdown>(OnEggRemoved);
    }

    private void OnEggAdded(Entity<EggHolderComponent> ent, ref ComponentInit args) => _eggs.TryAdd(ent.Owner, ent);

    private void OnEggRemoved(Entity<EggHolderComponent> ent, ref ComponentShutdown args) => _eggs.Remove(ent.Owner);

    public override void Update(float frameTime)
    {
        _blunt ??= _prototype.Index<DamageTypePrototype>("Blunt");
        _damage ??= new(_blunt, 1);

        _accumulatedTime += frameTime;

        if (_accumulatedTime >= HatchInterval)
        {
            _accumulatedTime -= HatchInterval;

            foreach (var egg in _eggs.Values)
            {
                _damageable.TryChangeDamage(egg.Owner, _damage, false);
                HatchEgg(egg.Owner);
            }
        }
    }

    private void HatchEgg(EntityUid eggUid)
    {
        var entity = EntityManager.SpawnEntity(_random.Pick(_terrorSpiders), Transform(eggUid).Coordinates);
        RemComp<EggHolderComponent>(eggUid);
    }
}
