using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Damage;
using Content.Server.ADT.Mech.Equipment.Components;

namespace Content.Server.ADT.Mech.Equipment.EntitySystems;

public sealed class MechArmorSystem : EntitySystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<MechArmorComponent, MechEquipmentInsertedEvent>(OnEquipmentInstalled);
        SubscribeLocalEvent<MechArmorComponent, MechEquipmentRemovedEvent>(OnEquipmentRemoved);
    }

    private void OnEquipmentInstalled(EntityUid uid, MechArmorComponent component, ref MechEquipmentInsertedEvent args)
    {
        if (!TryComp<MechComponent>(args.Mech, out var mech))
            return;
        component.OriginalModifiers = mech.Modifiers;
        mech.Modifiers = component.Modifiers;
    }

    private void OnEquipmentRemoved(EntityUid uid, MechArmorComponent component, ref MechEquipmentRemovedEvent args)
    {
        if (!TryComp<MechComponent>(args.Mech, out var mech))
            return;
        mech.Modifiers = component.OriginalModifiers;
        component.OriginalModifiers = null;
    }
}
