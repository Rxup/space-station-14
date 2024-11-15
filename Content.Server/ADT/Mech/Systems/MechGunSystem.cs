using Content.Server.Mech.Systems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Random;
using Content.Shared.Stunnable;
using Robust.Shared.Containers;
using Content.Shared.ADT.Mech.Equipment.Components;
using Content.Shared.ADT.Weapons.Ranged.Components;
using Content.Shared.Mech;
using Content.Shared.ADT.Mech;
using Robust.Shared.Timing;
using Robust.Server.Audio;
using Content.Server.ADT.Mech.Equipment.Components;

namespace Content.Server.ADT.Mech.Equipment.EntitySystems;
public sealed class MechGunSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly MechSystem _mech = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechEquipmentComponent, GunShotEvent>(MechGunShot);

        SubscribeLocalEvent<BallisticMechAmmoProviderComponent, MechEquipmentUiStateReadyEvent>(OnUiStateReady);
        SubscribeLocalEvent<BallisticMechAmmoProviderComponent, MechEquipmentUiMessageRelayEvent>(OnReload);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BallisticMechAmmoProviderComponent>();
        while (query.MoveNext(out var uid, out var gun))
        {
            if (gun.Reloading && gun.ReloadEnd <= _timing.CurTime)
            {
                gun.Reloading = false;
                gun.Shots = gun.Capacity;
                Dirty(uid, gun);
                _mech.UpdateUserInterfaceByEquipment(uid);
            }
        }
    }
    private void MechGunShot(EntityUid uid, MechEquipmentComponent component, ref GunShotEvent args)
    {
        if (!component.EquipmentOwner.HasValue)
        {
            if (TryComp<MechComponent>(args.User, out var pilot) && pilot.PilotSlot.ContainedEntity != null)
                _mech.TryEject(args.User, pilot);
            _stun.TryParalyze(args.User, TimeSpan.FromSeconds(10), true);
            _throwing.TryThrow(args.User, _random.NextVector2(), _random.Next(50));
            return;
        }
        if (!TryComp<MechComponent>(component.EquipmentOwner.Value, out var mech))
            return;

        _mech.UpdateUserInterface(component.EquipmentOwner.Value);

        // In most guns the ammo itself isn't shot but turned into cassings
        // and a new projectile is spawned instead, meaning that args.Ammo
        // is most likely inside the equipment container (for some odd reason)

        // I'm not even sure why this is needed since GunSystem.Shoot() has a
        // container check before ejecting, but yet it still puts the spent ammo inside the mech
        foreach (var (ent, _) in args.Ammo)
        {
            if (ent.HasValue && mech.EquipmentContainer.Contains(ent.Value))
            {
                _container.Remove(ent.Value, mech.EquipmentContainer);
                _throwing.TryThrow(ent.Value, _random.NextVector2(), _random.Next(5));
            }
        }
    }

    private void OnUiStateReady(EntityUid uid, BallisticMechAmmoProviderComponent component, MechEquipmentUiStateReadyEvent args)
    {
        var state = new MechGunUiState
        {
            ReloadTime = component.ReloadTime,
            Shots = component.Shots,
            Capacity = component.Capacity,
            Reloading = component.Reloading,
            ReloadEndTime = component.Reloading ? component.ReloadEnd : null,
        };
        args.States.Add(GetNetEntity(uid), state);
    }

    private void OnReload(EntityUid uid, BallisticMechAmmoProviderComponent comp, MechEquipmentUiMessageRelayEvent args)
    {
        if (args.Message is not MechGunReloadMessage msg)
            return;
        if (comp.Reloading)
            return;
        if (!TryComp<MechEquipmentComponent>(uid, out var equip) || !equip.EquipmentOwner.HasValue)
            return;
        if (!_timing.IsFirstTimePredicted)
        {
            _mech.UpdateUserInterfaceByEquipment(uid);
            return;
        }
        if (comp.Shots >= comp.Capacity)
        {
            _mech.UpdateUserInterfaceByEquipment(uid);
            return;
        }

        var magazine = TryMagazine(equip.EquipmentOwner.Value, comp);
        if (magazine == null || !_mech.TryChangeEnergy(equip.EquipmentOwner.Value, -comp.Capacity))
        {
            var pilot = GetEntity(args.Pilot) ?? EntityUid.Invalid;
            _audio.PlayPredicted(comp.NoAmmoForReload, pilot, equip.EquipmentOwner.Value);
            _mech.UpdateUserInterfaceByEquipment(uid);
            return;
        }
        else if (magazine != null)
            QueueDel(magazine);

        comp.Reloading = true;
        comp.ReloadEnd = _timing.CurTime + TimeSpan.FromSeconds(comp.ReloadTime);
        _audio.PlayPvs(comp.ReloadSound, uid);
        Dirty(uid, comp);
        _mech.UpdateUserInterfaceByEquipment(uid);
    }

    private EntityUid? TryMagazine(EntityUid mech, BallisticMechAmmoProviderComponent comp)
    {
        _container.TryGetContainer(mech, comp.AmmoContainerId, out var mechcontainer);

        if (mechcontainer == null)
            return null;

        foreach (var magazine in mechcontainer.ContainedEntities)
        {
            if (!TryComp<MechMagazineComponent>(magazine, out var magazinecomp))
                continue;
            if (comp.AmmoType != magazinecomp.MagazineType)
                continue;
            QueueDel(magazine);
            return magazine;
        }
        return null;
    }
}
