using Content.Shared.Backmen.Magic.Events;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Damage;

namespace Content.Shared.Backmen.Magic;

public abstract class SharedBkmMagicSystem : EntitySystem
{
    [Dependency] protected readonly DamageableSystem DamageableSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HealSpellEvent>(OnHealSpell);
    }

    private void OnHealSpell(HealSpellEvent ev)
    {
        if (ev.Handled)
            return;

        if (!HasComp<BodyComponent>(ev.Target))
            return;

        DamageableSystem.TryChangeDamage(ev.Target, ev.HealAmount, true, origin: ev.Target, targetPart: TargetBodyPart.All); // backmen: surgery
    }
}
