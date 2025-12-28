using Content.Shared._Backmen.Standing;
using Content.Shared._Orion.TelescopicBaton.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Examine;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;

namespace Content.Shared._Orion.TelescopicBaton.Systems;

public sealed class TelescopicBatonSystem : EntitySystem
{
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly ItemToggleSystem _itemToggle = default!;
    [Dependency] private readonly SharedLayingDownSystem _layingDown = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TelescopicBatonComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<TelescopicBatonComponent, StaminaDamageOnHitAttemptEvent>(OnStaminaHitAttempt);
        SubscribeLocalEvent<TelescopicBatonComponent, ItemToggledEvent>(ToggleDone);
        SubscribeLocalEvent<TelescopicBatonComponent, StaminaMeleeHitEvent>(OnHit);
    }

    private void OnHit(Entity<TelescopicBatonComponent> ent, ref StaminaMeleeHitEvent args)
    {
        foreach (var (uid, _) in args.HitList)
        {
            if (TryComp<LayingDownComponent>(uid, out var layingDownComponent))
            {
                _layingDown.TryLieDown(uid, layingDownComponent);
            }
        }
    }

    private void OnStaminaHitAttempt(Entity<TelescopicBatonComponent> entity, ref StaminaDamageOnHitAttemptEvent args)
    {
        if (!_itemToggle.IsActivated(entity.Owner))
            args.Cancelled = true;
    }

    private void OnExamined(Entity<TelescopicBatonComponent> entity, ref ExaminedEvent args)
    {
        var onMsg = _itemToggle.IsActivated(entity.Owner)
            ? Loc.GetString("comp-telebaton-examined-on")
            : Loc.GetString("comp-telebaton-examined-off");
        args.PushMarkup(onMsg);
    }

    private void ToggleDone(Entity<TelescopicBatonComponent> entity, ref ItemToggledEvent args)
    {
        _item.SetHeldPrefix(entity.Owner, args.Activated ? "on" : "off");
    }
}
