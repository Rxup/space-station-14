using Content.Shared.Inventory.Events;
using Content.Shared.Clothing.Components;
using Content.Shared.StatusEffectNew;

namespace Content.Shared.Backmen.Abilities.Psionics;

public sealed class PsionicItemsSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly SharedPsionicAbilitiesSystem _psiAbilities = default!;
    [Dependency] private readonly SharedEyeSystem _sharedEyeSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TinfoilHatComponent, GotEquippedEvent>(OnTinfoilEquipped);
        SubscribeLocalEvent<TinfoilHatComponent, GotUnequippedEvent>(OnTinfoilUnequipped);
        SubscribeLocalEvent<ClothingGrantPsionicPowerComponent, GotEquippedEvent>(OnGranterEquipped);
        SubscribeLocalEvent<ClothingGrantPsionicPowerComponent, GotUnequippedEvent>(OnGranterUnequipped);
    }
    private void OnTinfoilEquipped(EntityUid uid, TinfoilHatComponent component, GotEquippedEvent args)
    {
        // This only works on clothing
        if (!TryComp<ClothingComponent>(uid, out var clothing))
            return;
        // Is the clothing in its actual slot?
        if (!clothing.Slots.HasFlag(args.SlotFlags))
            return;

        var insul = EnsureComp<PsionicInsulationComponent>(args.EquipTarget);
        insul.Passthrough = component.Passthrough;
        component.IsActive = true;
        _psiAbilities.SetPsionicsThroughEligibility(args.EquipTarget);

        // Visibility mask will be set automatically by OnGetVisMask event handler
        _sharedEyeSystem.RefreshVisibilityMask(args.EquipTarget);
    }

    private void OnTinfoilUnequipped(EntityUid uid, TinfoilHatComponent component, GotUnequippedEvent args)
    {
        if (!component.IsActive)
            return;

        if (!_statusEffects.HasStatusEffect(args.EquipTarget, "StatusEffectPsionicallyInsulated"))
            RemComp<PsionicInsulationComponent>(args.EquipTarget);

        component.IsActive = false;
        _psiAbilities.SetPsionicsThroughEligibility(args.EquipTarget);
        _sharedEyeSystem.RefreshVisibilityMask(args.EquipTarget);
    }

    private void OnGranterEquipped(EntityUid uid, ClothingGrantPsionicPowerComponent component, GotEquippedEvent args)
    {
        // This only works on clothing
        if (!TryComp<ClothingComponent>(uid, out var clothing))
            return;
        // Is the clothing in its actual slot?
        if (!clothing.Slots.HasFlag(args.SlotFlags))
            return;
        // does the user already has this power?
        var componentType = _componentFactory.GetRegistration(component.Power).Type;
        if (HasComp(args.EquipTarget, componentType))
            return;

        var newComponent = (Component) _componentFactory.GetComponent(componentType);
        AddComp(args.EquipTarget, newComponent);

        component.IsActive = true;
    }

    private void OnGranterUnequipped(EntityUid uid, ClothingGrantPsionicPowerComponent component, GotUnequippedEvent args)
    {
        if (!component.IsActive)
            return;

        component.IsActive = false;
        var componentType = _componentFactory.GetRegistration(component.Power).Type;
        if (EntityManager.HasComponent(args.EquipTarget, componentType))
        {
            EntityManager.RemoveComponent(args.EquipTarget, componentType);
        }
    }
}
