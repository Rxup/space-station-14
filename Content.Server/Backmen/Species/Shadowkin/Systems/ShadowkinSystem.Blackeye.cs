using Content.Server.Backmen.Species.Shadowkin.Components;
using Content.Shared.Backmen.Species.Shadowkin.Events;
using Content.Shared.Backmen.Species.Shadowkin.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;

namespace Content.Server.Backmen.Species.Shadowkin.Systems;

public sealed class ShadowkinBlackeyeSystem : EntitySystem
{
    [Dependency] private readonly ShadowkinPowerSystem _power = default!;
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShadowkinBlackeyeAttemptEvent>(OnBlackeyeAttempt);
        SubscribeAllEvent<ShadowkinBlackeyeEvent>(OnBlackeye);
    }


    private void OnBlackeyeAttempt(ShadowkinBlackeyeAttemptEvent ev)
    {
        if (!TryComp<ShadowkinComponent>(GetEntity(ev.Uid), out var component) ||
            component.Blackeye ||
            !(component.PowerLevel <= ShadowkinComponent.PowerThresholds[ShadowkinPowerThreshold.Min] + 5))
            ev.Cancel();
    }

    private void OnBlackeye(ShadowkinBlackeyeEvent ev)
    {
        var ent = GetEntity(ev.Uid);
        // Check if the entity is a shadowkin
        if (!TryComp<ShadowkinComponent>(ent, out var component))
            return;

        // Stop gaining power
        component.Blackeye = true;
        component.PowerLevelGainEnabled = false;
        _power.SetPowerLevel(ent, ShadowkinComponent.PowerThresholds[ShadowkinPowerThreshold.Min]);

        // Update client state
        Dirty(ent, component);

        // Remove powers
        _entity.RemoveComponent<ShadowkinDarkSwapPowerComponent>(ent);
        _entity.RemoveComponent<ShadowkinDarkSwappedComponent>(ent);
        _entity.RemoveComponent<ShadowkinRestPowerComponent>(ent);
        _entity.RemoveComponent<ShadowkinTeleportPowerComponent>(ent);

        if (!ev.Damage)
            return;

        // Popup
        _popup.PopupEntity(Loc.GetString("shadowkin-blackeye"),ent, ent, PopupType.Large);

        // Stamina crit
        if (_entity.TryGetComponent<StaminaComponent>(ent, out var stamina))
        {
            _stamina.TakeStaminaDamage(ent, stamina.CritThreshold, null, ent);
        }

        // Nearly crit with cellular damage
        // If already 5 damage off of crit, don't do anything
        if (!_entity.TryGetComponent<DamageableComponent>(ent, out var damageable) ||
            !_mobThreshold.TryGetThresholdForState(ent, MobState.Critical, out var key))
            return;

        var minus = damageable.TotalDamage;

        _damageable.TryChangeDamage(
            ent,
            new DamageSpecifier(_prototype.Index<DamageTypePrototype>("Cellular"),
                Math.Max((double) (key.Value - minus - 5), 0)),
            true,
            true,
            null,
            null
        );
    }
}
