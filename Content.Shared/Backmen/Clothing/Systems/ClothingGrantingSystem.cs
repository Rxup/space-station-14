using Content.Shared.Clothing.Components;
using Content.Shared.Inventory.Events;
using Robust.Shared.Serialization.Manager;
using Content.Shared.Tag;

namespace Content.Shared.Backmen.Clothing;

public sealed partial class ClothingGrantingSystem : EntitySystem
{
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private ISerializationManager _serializationManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClothingGrantComponent, GotEquippedEvent>(OnCompEquip);
        SubscribeLocalEvent<ClothingGrantComponent, GotUnequippedEvent>(OnCompUnequip);
    }

    private void OnCompEquip(EntityUid uid, ClothingGrantComponent component, GotEquippedEvent args)
    {
        if (!TryComp<ClothingComponent>(uid, out var clothing))
            return;

        if (!clothing.Slots.HasFlag(args.SlotFlags))
            return;

        if (component.Components.Count > 1)
        {
            Log.Error("Although a component registry supports multiple components, we cannot bookkeep more than 1 component for ClothingGrantComponent at this time.");
            return;
        }

        foreach (var (name, data) in component.Components)
        {
            var newComp = (Component) _componentFactory.GetComponent(name);

            if (HasComp(args.EquipTarget, newComp.GetType()))
                continue;

            var temp = (object) newComp;
            _serializationManager.CopyTo(data.Component, ref temp);
            EntityManager.AddComponent(args.EquipTarget, (Component)temp!);

            component.IsActive = true;
        }
    }

    private void OnCompUnequip(EntityUid uid, ClothingGrantComponent component, GotUnequippedEvent args)
    {
        if (!component.IsActive) return;

        foreach (var (name, data) in component.Components)
        {
            var newComp = (Component) _componentFactory.GetComponent(name);

            RemComp(args.EquipTarget, newComp.GetType());
        }

        component.IsActive = false;
    }
}
