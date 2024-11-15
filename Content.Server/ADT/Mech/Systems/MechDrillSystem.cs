using Content.Server.Destructible;
using Content.Server.Gatherable.Components;
using Content.Server.Interaction;
using Content.Server.Mech.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Robust.Shared.Audio.Systems;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Server.ADT.Mech.Equipment.Components;

namespace Content.Server.ADT.Mech.Equipment.EntitySystems;

/// <summary>
/// Handles <see cref="MechDrillComponent"/> and all related UI logic
/// </summary>
public sealed class MechDrillSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MechSystem _mech = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DestructibleSystem _destructible = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<MechDrillComponent, UserActivateInWorldEvent>(OnInteract);
        SubscribeLocalEvent<MechDrillComponent, MechDrillDoAfterEvent>(OnDoAfter);
    }

    /// <summary>
    /// When mecha driver uses the tool
    /// </summary>
    private void OnInteract(EntityUid uid, MechDrillComponent component, UserActivateInWorldEvent args)
    {
        if (args.Handled)
            return;
        var target = args.Target;

        if (!TryComp<MechComponent>(args.User, out var mech))
            return;

        if (mech.Energy + component.DrillEnergyDelta < 0)
            return;

        if (!_interaction.InRangeUnobstructed(args.User, target))
            return;

        args.Handled = true;
        component.Token = new();
        var damageRequired = _destructible.DestroyedAt(target);
        var damageTime = (damageRequired / component.DrillSpeedMultilire).Float();
        if (HasComp<GatherableComponent>(args.Target) || HasComp<MobStateComponent>(target))
            damageTime = 0.5f;
        var doAfter = new DoAfterArgs(EntityManager, args.User, damageTime, new MechDrillDoAfterEvent(), uid, target: target, used: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            MovementThreshold = 0.25f,
        };
        _audio.PlayPvs(component.DrillSound, uid);
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDoAfter(EntityUid uid, MechDrillComponent component, MechDrillDoAfterEvent args)
    {
        if (args?.Args?.Target is not { } target)
            return;

        if (args.Cancelled)
            return;
        component.Token = null;

        if (HasComp<GatherableComponent>(args.Target))
        {
            var xform = Transform(args.Target.Value);
            var gatherables = new HashSet<Entity<GatherableComponent>>();
            _lookup.GetEntitiesInRange(xform.Coordinates, 1, gatherables);

            foreach (var gatherable in gatherables)
            {
                var ent = gatherable.Owner;
                _damageable.TryChangeDamage(ent, component.DamageToDrilled, ignoreResistances: true);
            }
        }

        if (!TryComp<MechEquipmentComponent>(uid, out var equipmentComponent) || equipmentComponent.EquipmentOwner == null)
            return;

        if (!_mech.TryChangeEnergy(equipmentComponent.EquipmentOwner.Value, component.DrillEnergyDelta))
            return;
        if (Comp<MechComponent>(equipmentComponent.EquipmentOwner.Value).Energy <= 0)
            args.Repeat = false;

        _damageable.TryChangeDamage(args.Target, component.DamageToDrilled, ignoreResistances: false);
        _mech.UpdateUserInterface(equipmentComponent.EquipmentOwner.Value);
        args.Repeat = true;
    }
}
