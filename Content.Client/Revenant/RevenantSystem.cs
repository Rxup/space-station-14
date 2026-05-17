using Content.Client.Alerts;
using Content.Shared.Alert;
using Content.Shared.Alert.Components;
using Content.Shared._Impstation.Revenant;
using Content.Shared._Impstation.Revenant.Components;
using Content.Shared.Revenant;
using Content.Shared.Revenant.Components;
using Content.Shared.StatusEffectNew;
using Robust.Client.GameObjects;

namespace Content.Client.Revenant;

public sealed partial class RevenantSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private StatusEffectsSystem _status = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevenantComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<RevenantComponent, GetGenericAlertCounterAmountEvent>(OnGetCounterAmount);
    }

    private void OnAppearanceChange(EntityUid uid, RevenantComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (_appearance.TryGetData<bool>(uid, RevenantVisuals.Harvesting, out var harvesting, args.Component) && harvesting)
        {
            _sprite.LayerSetRsiState((uid, args.Sprite), 0, component.HarvestingState);
        }
        else if (_appearance.TryGetData<bool>(uid, RevenantVisuals.Stunned, out var stunned, args.Component) && stunned)
        {
            _sprite.LayerSetRsiState((uid, args.Sprite), 0, component.StunnedState);
        }
        else if (_appearance.TryGetData<bool>(uid, RevenantVisuals.Corporeal, out var corporeal, args.Component))
        {
            if (corporeal)
                _sprite.LayerSetRsiState((uid, args.Sprite), 0, component.CorporealState);
            else
                _sprite.LayerSetRsiState((uid, args.Sprite), 0, component.State);
        }
    }

    private void OnGetCounterAmount(Entity<RevenantComponent> ent, ref GetGenericAlertCounterAmountEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.EssenceAlert == args.Alert)
        {
            args.Amount = ent.Comp.Essence.Int();
            return;
        }

        if (args.Alert.ID != "EssenceRegen")
            return;

        if (!_status.TryGetStatusEffect(ent, RevenantStatusEffects.EssenceRegen, out var regenEnt)
            || !TryComp<RevenantRegenModifierStatusEffectComponent>(regenEnt, out var regen))
            return;

        args.Amount = regen.Witnesses.Count;
    }
}
