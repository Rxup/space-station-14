using Content.Shared.Inventory.Events;
using Content.Shared.Clothing.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Abilities.Psionics;

public sealed partial class PsionicItemsSystem : EntitySystem
{
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private SharedPsionicAbilitiesSystem _psiAbilities = default!;
    [Dependency] private SharedEyeSystem _sharedEyeSystem = default!;


    private static readonly EntProtoId<PsionicInsulationComponent> StatusEffectPsionicallyInsulated = "StatusEffectPsionicallyInsulated";

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

        if (!_statusEffects.TrySetStatusEffectDuration(args.Equipee, StatusEffectPsionicallyInsulated, out var effect))
            return;
        var insul = EnsureComp<PsionicInsulationComponent>(effect.Value);
        insul.Passthrough = component.Passthrough;
        component.IsActive = true;
        _psiAbilities.SetPsionicsThroughEligibility(args.Equipee);

        // Visibility mask will be set automatically by OnGetVisMask event handler
        _sharedEyeSystem.RefreshVisibilityMask(args.Equipee);
    }

    private void OnTinfoilUnequipped(EntityUid uid, TinfoilHatComponent component, GotUnequippedEvent args)
    {
        if (!component.IsActive)
            return;

        _statusEffects.TryRemoveStatusEffect(args.Equipee, StatusEffectPsionicallyInsulated);
        component.IsActive = false;
        _psiAbilities.SetPsionicsThroughEligibility(args.Equipee);
        _sharedEyeSystem.RefreshVisibilityMask(args.Equipee);
    }

    private void OnGranterEquipped(EntityUid uid, ClothingGrantPsionicPowerComponent component, GotEquippedEvent args)
    {
        // This only works on clothing
        if (!TryComp<ClothingComponent>(uid, out var clothing))
            return;

        // Is the clothing in its actual slot?
        if (!clothing.Slots.HasFlag(args.SlotFlags))
            return;

        if (_statusEffects.HasStatusEffect(args.Equipee, component.StatusEffect))
            return;

        if (!_statusEffects.TrySetStatusEffectDuration(args.Equipee, component.StatusEffect))
            return;

        component._hasEffect = true;
    }

    private void OnGranterUnequipped(EntityUid uid, ClothingGrantPsionicPowerComponent component, GotUnequippedEvent args)
    {
        if (!component._hasEffect)
            return;

        component._hasEffect = false;
        _statusEffects.TryRemoveStatusEffect(args.Equipee, component.StatusEffect);
    }
}
