using Content.Server.Atmos.Components;
using Content.Shared.Heretic;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.Damage;
using Content.Shared.Atmos;
using Content.Server.Polymorph.Systems;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Server.Heretic.Abilities;

public sealed partial class HereticAbilitySystem
{
    private void SubscribeAsh()
    {
        SubscribeLocalEvent<HereticComponent, EventHereticAshenShift>(OnJaunt);
        SubscribeLocalEvent<GhoulComponent, EventHereticAshenShift>(OnJauntGhoul);
        SubscribeLocalEvent<HereticComponent, PolymorphRevertEvent>(OnJauntEnd);

        SubscribeLocalEvent<HereticComponent, EventHereticVolcanoBlast>(OnVolcano);
        SubscribeLocalEvent<HereticComponent, EventHereticNightwatcherRebirth>(OnNWRebirth);
        SubscribeLocalEvent<HereticComponent, EventHereticFlames>(OnFlames);
        SubscribeLocalEvent<HereticComponent, EventHereticCascade>(OnCascade);
    }

    private void OnJaunt(Entity<HereticComponent> ent, ref EventHereticAshenShift args)
    {
        if (TryDoJaunt(ent))
            args.Handled = true;
    }

    private void OnJauntGhoul(Entity<GhoulComponent> ent, ref EventHereticAshenShift args)
    {
        if (TryDoJaunt(ent))
            args.Handled = true;
    }

    [ValidatePrototypeId<EntityPrototype>]
    private const string PolymorphAshJauntAnimation = "PolymorphAshJauntAnimation";

    [ValidatePrototypeId<PolymorphPrototype>]
    private const string AshJaunt = "AshJaunt";

    private bool TryDoJaunt(EntityUid ent)
    {
        Spawn(PolymorphAshJauntAnimation, Transform(ent).Coordinates);
        var urist = _poly.PolymorphEntity(ent, AshJaunt);
        return urist != null;
    }

    [ValidatePrototypeId<EntityPrototype>]
    private const string PolymorphAshJauntEndAnimation = "PolymorphAshJauntEndAnimation";

    private void OnJauntEnd(Entity<HereticComponent> ent, ref PolymorphRevertEvent args)
    {
        Spawn(PolymorphAshJauntEndAnimation, Transform(ent).Coordinates);
    }

    private void OnVolcano(Entity<HereticComponent> ent, ref EventHereticVolcanoBlast args)
    {
        var ignoredTargets = new List<EntityUid>();
        // all ghouls are immune to heretic shittery
        var ghoulQuery = EntityQueryEnumerator<GhoulComponent>();
        while (ghoulQuery.MoveNext(out var owner, out _))
        {
            ignoredTargets.Add(owner);
        }

        // all heretics with the same path are also immune
        var hereticQuery = EntityQueryEnumerator<HereticComponent>();
        while (hereticQuery.MoveNext(out var owner, out var comp))
        {
            if (comp.CurrentPath == ent.Comp.CurrentPath)
                ignoredTargets.Add(owner);
        }

        if (!_splitball.Spawn(ent, ignoredTargets))
            return;

        if (ent.Comp.Ascended) // will only work on ash path
            _flammable.AdjustFireStacks(ent, 20f, ignite: true);

        args.Handled = true;
    }

    private HashSet<Entity<FlammableComponent>> _rebirthEntities = new();

    private void OnNWRebirth(Entity<HereticComponent> ent, ref EventHereticNightwatcherRebirth args)
    {
        _rebirthEntities.Clear();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, 5f, _rebirthEntities);

        foreach (var look in _rebirthEntities)
        {
            if ((_hereticQuery.TryComp(look, out var th) && th.CurrentPath == ent.Comp.CurrentPath)
                || _ghoulQuery.HasComp(look))
                continue;

            var flam = look.Comp;

            if (flam.OnFire && TryComp<DamageableComponent>(ent, out var dmgc))
            {
                // heals everything by 10 for each burning target
                _stam.TryTakeStamina(ent, -10);
                var damageDict = dmgc.Damage.DamageDict;
                foreach (var key in damageDict.Keys)
                {
                    damageDict[key] = -10f;
                }

                var damageSpecifier = new DamageSpecifier() { DamageDict = damageDict };
                _dmg.TryChangeDamage(ent, damageSpecifier, true, false, dmgc);
            }

            if (!flam.OnFire)
                _flammable.AdjustFireStacks(look, 5, flam, true);

            if (TryComp<MobStateComponent>(look, out var mobStateComponent))
            {
                if (mobStateComponent.CurrentState == MobState.Critical)
                    _mobstate.ChangeMobState(look, MobState.Dead, mobStateComponent);
            }
        }

        args.Handled = true;
    }

    private void OnFlames(Entity<HereticComponent> ent, ref EventHereticFlames args)
    {
        EnsureComp<HereticFlamesComponent>(ent);

        if (ent.Comp.Ascended)
            _flammable.AdjustFireStacks(ent, 20f, ignite: true);

        args.Handled = true;
    }

    private void OnCascade(Entity<HereticComponent> ent, ref EventHereticCascade args)
    {
        var entTransform = Transform(ent);

        if (!entTransform.GridUid.HasValue)
            return;

        // yeah. it just generates a ton of plasma which just burns.
        // lame, but we don't have anything fire related atm, so, it works.
        var tilePosition = _xform.GetGridOrMapTilePosition(ent, entTransform);
        var enumerator = _atmos.GetAdjacentTileMixtures(entTransform.GridUid!.Value, tilePosition, false, false);
        while (enumerator.MoveNext(out var mix))
        {
            mix.AdjustMoles(Gas.Plasma, 50f);
            mix.Temperature = Atmospherics.T0C + 125f;
        }

        if (ent.Comp.Ascended)
            _flammable.AdjustFireStacks(ent, 20f, ignite: true);

        args.Handled = true;
    }
}
