using System.Diagnostics.CodeAnalysis;
using Content.Shared.Backmen.Surgery.Effects.Step;
using Content.Shared.Body;
using Content.Shared.Body.Organ;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Explosion;
using Robust.Shared.Containers;

namespace Content.Server.Backmen.Surgery;

/// <summary>
/// Ensures explosives hidden in surgery cavities damage the host body and propagate through explosion recursion.
/// </summary>
public sealed class SurgeryCavityExplosionSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, BeforeExplodeEvent>(OnBodyBeforeExplode);
    }

    private void OnBodyBeforeExplode(Entity<BodyComponent> ent, ref BeforeExplodeEvent args)
    {
        if (ent.Comp.Organs == null)
            return;

        foreach (var organ in ent.Comp.Organs.ContainedEntities)
        {
            if (!TryComp<ItemSlotsComponent>(organ, out var itemSlots)
                || !_itemSlots.TryGetSlot(organ, SurgeryStepCavityEffectComponent.SlotId, out var slot, itemSlots)
                || slot.Item is not { } item)
            {
                continue;
            }

            args.Contents.Add(item);
        }
    }

    public bool TryGetSurgeryCavityHost(
        EntityUid item,
        [NotNullWhen(true)] out EntityUid body,
        [NotNullWhen(true)] out EntityUid organ)
    {
        body = default;
        organ = default;

        if (!_containers.TryGetContainingContainer(item, out var container))
            return false;

        var holder = container.Owner;
        if (!TryComp<OrganComponent>(holder, out var organComp)
            || organComp.Body is not { } hostBody
            || !TryComp<ItemSlotsComponent>(holder, out var itemSlots)
            || !_itemSlots.TryGetSlot(holder, SurgeryStepCavityEffectComponent.SlotId, out var slot, itemSlots)
            || slot.ContainerSlot != container)
        {
            return false;
        }

        organ = holder;
        body = hostBody;
        return true;
    }
}
