using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared.Backmen.SawCleaver;

public sealed class SawCleaverSystem : EntitySystem
{
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly ItemToggleSystem _itemToggle = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SawCleaverComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<SawCleaverComponent, ItemToggledEvent>(ToggleDone);
        SubscribeLocalEvent<SawCleaverComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<SawCleaverComponent> ent, ref MeleeHitEvent args)
    {
        var (uid, component) = ent;
        var attacker = args.User;

        foreach (var hit in args.HitEntities)
        {
            if (TryComp<MobStateComponent>(hit, out var mobState) && !_mobStateSystem.IsDead(hit))
            {
                _damageableSystem.TryChangeDamage(hit, args.BaseDamage);

                if (component.HealOnHit != null)
                {
                    _damageableSystem.TryChangeDamage(attacker, component.HealOnHit);
                }
            }
        }
    }

    private void OnExamined(Entity<SawCleaverComponent> entity, ref ExaminedEvent args)
    {
        var onMsg = _itemToggle.IsActivated(entity.Owner)
            ? Loc.GetString("saw-cleaver-examined-on")
            : Loc.GetString("saw-cleaver-examined-off");
        args.PushMarkup(onMsg);
    }

    private void ToggleDone(Entity<SawCleaverComponent> entity, ref ItemToggledEvent args)
    {
        _item.SetHeldPrefix(entity.Owner, args.Activated ? "on" : "off");
    }
}
