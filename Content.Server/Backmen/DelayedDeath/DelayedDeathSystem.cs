using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;
namespace Content.Server.Backmen.DelayedDeath;

public sealed partial class DelayedDeathSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private static readonly ProtoId<DamageTypePrototype> Bloodloss = "Bloodloss";

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        using var query = EntityQueryEnumerator<DelayedDeathComponent>();
        while (query.MoveNext(out var ent, out var component))
        {
            component.DeathTimer += frameTime;

            if (component.DeathTimer >= component.DeathTime && !_mobState.IsDead(ent))
            {
                var damage = new DamageSpecifier(_prototypes.Index<DamageTypePrototype>(Bloodloss), 150);
                _damageable.ChangeDamage(ent, damage, partMultiplier: 0f);
            }
        }
    }
}
