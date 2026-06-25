using Content.Shared.Backmen.Magic.Events;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;

namespace Content.Shared.Backmen.Magic;

public abstract partial class SharedBkmMagicSystem : EntitySystem
{
    [Dependency] protected DamageableSystem DamageableSystem = default!;

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

        DamageableSystem.ChangeDamage(ev.Target, ev.HealAmount, true, origin: ev.Target, targetPart: TargetBodyPart.All); // backmen: surgery
    }
}
