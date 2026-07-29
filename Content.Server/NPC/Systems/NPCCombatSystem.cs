using Content.Server.Interaction;
using Content.Server.Wieldable;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Storage.Components; // backmen: entity-storage-combat
using Content.Shared.Weapons.Melee;
using Robust.Server.Containers; // backmen: entity-storage-combat
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.NPC.Systems;

/// <summary>
/// Handles combat for NPCs.
/// </summary>
public sealed partial class NPCCombatSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private GunSystem _gun = default!;
    [Dependency] private InteractionSystem _interaction = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private NPCSteeringSystem _steering = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private WieldableSystem _wield = default!;
    [Dependency] private readonly ContainerSystem _container = default!; // backmen: entity-storage-combat
    [Dependency] private readonly IPrototypeManager _prototype = default!; // backmen: entity-storage-combat

    /// <summary>
    /// If disabled we'll move into range but not attack.
    /// </summary>
    public bool Enabled = true;

    public override void Initialize()
    {
        base.Initialize();
        InitializeMelee();
        InitializeRanged();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateMelee(frameTime);
        UpdateRanged(frameTime);
    }

    // start-backmen: entity-storage-combat
    /// <summary>
    /// Attack EntityStorage instead of a contained target, but only if our damage can hurt the storage.
    /// </summary>
    private EntityUid GetAttackTarget(EntityUid attacker, EntityUid target)
    {
        if (!_container.TryGetContainingContainer(target, out var container) ||
            !HasComp<EntityStorageComponent>(container.Owner))
            return target;

        if (!CanDamageEntityStorage(attacker, container.Owner))
            return target;

        return container.Owner;
    }

    /// <summary>
    /// Whether this attacker can deal damage to an EntityStorage (crate/locker).
    /// </summary>
    public bool CanDamageEntityStorage(EntityUid attacker, EntityUid storage)
    {
        if (!TryComp<DamageableComponent>(storage, out var damageable))
            return false;

        // Guns / rockets can break crates.
        if (_gun.TryGetGun(attacker, out _))
            return true;

        if (!_melee.TryGetWeapon(attacker, out var weaponUid, out var weapon))
            return false;

        var damage = _melee.GetDamage(weaponUid, attacker, weapon);
        if (damage.Empty)
            return false;

        if (damageable.DamageModifierSetId != null &&
            _prototype.Resolve(damageable.DamageModifierSetId, out DamageModifierSetPrototype? modifiers))
        {
            damage = DamageSpecifier.ApplyModifierSet(damage, modifiers);
        }

        return damage.GetTotal() > 0;
    }
    // end-backmen: entity-storage-combat
}
