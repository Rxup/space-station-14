using Content.Shared.Materials;
using Content.Shared.Interaction.Events;
using Content.Server.Hands.Systems;
using Content.Server.Materials;
using Content.Server.Power.EntitySystems;
using Content.Server.Power.Components;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;

namespace Content.Server._Goobstation.Plasmacutter;

public sealed class BatteryRechargeSystem : EntitySystem
{
    [Dependency] private readonly MaterialStorageSystem _materialStorage = default!;
    [Dependency] private readonly SharedBatterySystem _batterySystem = default!;
    [Dependency] private readonly HandsSystem _hands = default!;

    private EntityUid playerUid;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MaterialStorageComponent, ContactInteractionEvent>(OnInteract);
        SubscribeLocalEvent<MaterialStorageComponent, MaterialEntityInsertedEvent>(OnMaterialAmountChanged);
        SubscribeLocalEvent<BatteryRechargeComponent, ChargeChangedEvent>(OnChargeChanged);
    }

    private void OnInteract(EntityUid uid, MaterialStorageComponent component, ContactInteractionEvent args)
    {
        playerUid = args.Other;
    }

    private void OnMaterialAmountChanged(EntityUid uid, MaterialStorageComponent component, MaterialEntityInsertedEvent args)
    {
        if (component.MaterialWhiteList != null)
            foreach (var fuelType in component.MaterialWhiteList)
            {
                FuelAddCharge(uid, fuelType);
            }
    }

    private void OnChargeChanged(EntityUid uid, BatteryRechargeComponent component, ChargeChangedEvent args)
    {
        ChangeStorageLimit(uid, component.StorageMaxCapacity);
    }

    private void ChangeStorageLimit(
        Entity<BatteryComponent?> ent,
        int value)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        if (_batterySystem.GetCharge(ent) == ent.Comp.MaxCharge)
            value = 0;
        _materialStorage.TryChangeStorageLimit(ent, value);
    }

    private void FuelAddCharge(
        Entity<BatteryComponent?> ent,
        string fuelType)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var availableMaterial = _materialStorage.GetMaterialAmount(ent, fuelType);

        if (_materialStorage.TryChangeMaterialAmount(ent, fuelType, -availableMaterial))
        {
            // this is shit. this shit works.
            var spawnAmount = ent.Comp.MaxCharge - _batterySystem.GetCharge(ent) - availableMaterial;
            if (spawnAmount < 0)
            {
                spawnAmount = Math.Abs(spawnAmount);
            }
            else
            {
                spawnAmount = 0;
            }

            var ents = _materialStorage.SpawnMultipleFromMaterial((int)spawnAmount, fuelType, Transform(ent).Coordinates, out var overflow);

            foreach (var entUid in ents)
            {
                _hands.TryForcePickupAnyHand(playerUid, entUid);
            }

            _batterySystem.ChangeCharge(ent, availableMaterial);
        }
    }
}
