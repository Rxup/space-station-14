using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Robust.Server.GameObjects;
using Content.Server.Emp;
using Content.Shared.Mech.Equipment.Components;

namespace Content.Server.Mech.Systems;

/// <inheritdoc/>
public sealed partial class MechSystem : SharedMechSystem
{
    private void InitializeADT()
    {
        SubscribeLocalEvent<MechComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<MechComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<MechComponent, MechEquipmentDestroyedEvent>(OnEquipmentDestroyed);
        SubscribeLocalEvent<MechComponent, MechTurnLightsEvent>(OnTurnLightsEvent);
        SubscribeLocalEvent<MechComponent, MechInhaleEvent>(OnToggleInhale);
    }

    private void OnToggleInhale(EntityUid uid, MechComponent component, MechInhaleEvent args)
    {
        if (component.Airtight)
        {
            component.Airtight = false;
            return;
        }
        component.Airtight = true;
    }

    private void OnDamageModify(EntityUid uid, MechComponent component, DamageModifyEvent args)
    {
        if (component.Modifiers != null)
            args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, component.Modifiers);
    }

    private void OnTurnLightsEvent(EntityUid uid, MechComponent component, MechTurnLightsEvent args)
    {
        if (HasComp<PointLightComponent>(uid))
        {
            RemComp<PointLightComponent>(uid);
            _audio.PlayPvs(component.MechLightsOffSound, uid);
        }
        else
        {
            AddComp<PointLightComponent>(uid);
            _audio.PlayPvs(component.MechLightsOnSound, uid);
        }
    }

    private void OnEquipmentDestroyed(EntityUid uid, MechComponent component, ref MechEquipmentDestroyedEvent args)
    {
        Spawn("EffectSparks", Transform(uid).Coordinates);
        QueueDel(component.CurrentSelectedEquipment);
        _audio.PlayPvs(component.EquipmentDestroyedSound, uid);
    }

    private void OnEmpPulse(EntityUid uid, MechComponent comp, ref EmpPulseEvent args)
    {
        var damage = args.EnergyConsumption / 100;
        TryChangeEnergy(uid, -FixedPoint2.Min(comp.Energy, damage), comp);
        Spawn("EffectEmpPulse", Transform(uid).Coordinates);
    }

    public override void UpdateUserInterfaceByEquipment(EntityUid equipmentUid)
    {
        base.UpdateUserInterfaceByEquipment(equipmentUid);

        if (!TryComp<MechEquipmentComponent>(equipmentUid, out var comp))
        {
            Log.Error("Could not find mech equipment owner to update UI.");
            return;
        }
        if (!comp.EquipmentOwner.HasValue)
            return;
        UpdateUserInterface(comp.EquipmentOwner.Value);
    }
}
