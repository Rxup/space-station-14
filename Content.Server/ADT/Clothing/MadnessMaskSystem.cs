using Content.Server.EntityEffects.Effects;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Drugs;
using Content.Shared.Drunk;
using Content.Shared.Heretic;
using Content.Shared.Jittering;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Robust.Shared.Random;

namespace Content.Server.Goobstation.Clothing;

public sealed partial class MadnessMaskSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private EntityQuery<HereticComponent> _hereticQuery;
    private EntityQuery<GhoulComponent> _ghoulQuery;
    private EntityQuery<StaminaComponent> _staminaQuery;
    private HashSet<EntityUid> _maskEffectEntities = new();

    public override void Initialize()
    {
        base.Initialize();
        _hereticQuery = GetEntityQuery<HereticComponent>();
        _ghoulQuery = GetEntityQuery<GhoulComponent>();
        _staminaQuery = GetEntityQuery<StaminaComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MadnessMaskComponent>();

        while (query.MoveNext(out var owner, out var mask))
        {
            mask.UpdateAccumulator += frameTime;
            if (mask.UpdateAccumulator < mask.UpdateTimer)
                continue;

            mask.UpdateAccumulator = 0;

            _maskEffectEntities.Clear();
            _lookup.GetEntitiesInRange(owner, 5f, _maskEffectEntities);
            foreach (var look in _maskEffectEntities)
            {
                // heathens exclusive
                if (_hereticQuery.HasComp(look) || _ghoulQuery.HasComp(look))
                    continue;

                if (_random.Prob(.4f) && _staminaQuery.TryComp(look, out var staminaComp))
                    _stamina.TakeStaminaDamage(look, 5f, staminaComp, visual: false);

                if (_random.Prob(.4f))
                    _jitter.DoJitter(look, TimeSpan.FromSeconds(.5f), true, amplitude: 5, frequency: 10);

                if (_random.Prob(.25f))
                    _statusEffect.TryAddStatusEffect<SeeingRainbowsComponent>(look, "SeeingRainbows", TimeSpan.FromSeconds(10f), false);
            }
        }
    }
}
