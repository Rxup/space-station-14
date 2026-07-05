using Content.Server.Atmos.Rotting;
using Content.Server.Backmen.Surgery.Trauma.Systems;
using Robust.Shared.Random;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Backmen.Body;
using Content.Shared.Backmen.Body.Components;
using Content.Shared.Backmen.Surgery.Body.Organs;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Body;
using Content.Shared.Gibbing;

namespace Content.Server.Backmen.Body.Systems;

/// <summary>
/// Drop chance, harvest damage, and perish setup for cosmic carp organs on gib.
/// </summary>
public sealed class SpaceAnimalOrganSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ServerTraumaSystem _trauma = default!;
    [Dependency] private RottingSystem _rotting = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpaceAnimalOrganComponent, BodyRelayedEvent<BeingGibbedEvent>>(
            OnBeingGibbed,
            after: [typeof(GibbableOrganSystem)]);

        SubscribeLocalEvent<SpaceAnimalOrganComponent, OrganHarvestDamageEvent>(OnHarvestDamage);
    }

    private void OnBeingGibbed(
        Entity<SpaceAnimalOrganComponent> ent,
        ref BodyRelayedEvent<BeingGibbedEvent> args)
    {
        // start-backmen: space-animal-organs
        if (_random.NextFloat() > ent.Comp.DropChance)
        {
            args.Args.Giblets.Remove(ent.Owner);
            return;
        }

        var ev = new OrganHarvestDamageEvent(ent.Comp.HarvestDamageFraction);
        RaiseLocalEvent(ent.Owner, ref ev);
        // end-backmen: space-animal-organs
    }

    private void OnHarvestDamage(Entity<SpaceAnimalOrganComponent> ent, ref OrganHarvestDamageEvent args)
    {
        if (!TryComp<OrganComponent>(ent, out var organ))
            return;

        // start-backmen: space-animal-organs
        var damage = organ.IntegrityCap * args.Fraction;
        if (damage > 0)
        {
            if (!_trauma.TrySetOrganDamageModifier(ent, damage, ent, "HarvestDamage", organ))
                _trauma.TryAddOrganDamageModifier(ent, damage, ent, "HarvestDamage", organ);
        }

        var perishable = _rotting.StartOrganHarvestPerish(ent, ent.Comp.OrganRotAfter);
        Dirty(ent, perishable);
        // end-backmen: space-animal-organs
    }
}
