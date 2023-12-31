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
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShadowkinComponent,ShadowkinBlackeyeAttemptEvent>(OnBlackeyeAttempt);
        SubscribeLocalEvent<ShadowkinBlackeyeTraitComponent, ComponentStartup>(OnStartupTrait);
    }

    private void OnBlackeyeAttempt(Entity<ShadowkinComponent> ent, ref ShadowkinBlackeyeAttemptEvent ev)
    {
        if (!TryComp<ShadowkinComponent>(ent, out var component) ||
            component.Blackeye ||
            !(component.PowerLevel <= ShadowkinComponent.PowerThresholds[ShadowkinPowerThreshold.Min] + 5))
            ev.Cancel();
    }

    private void OnStartupTrait(Entity<ShadowkinBlackeyeTraitComponent> ent, ref ComponentStartup args)
    {
        SetBlackEye(ent);
    }

    public void SetBlackEye(EntityUid ent, bool damage = false)
    {
        // Check if the entity is a shadowkin
        if (!TryComp<ShadowkinComponent>(ent, out var component) || component.Blackeye)
            return;

        // Stop gaining power
        component.Blackeye = true;
        component.PowerLevelGainEnabled = false;
        _power.SetPowerLevel(ent, ShadowkinComponent.PowerThresholds[ShadowkinPowerThreshold.Min]);

        // Update client state
        Dirty(ent, component);

        // Remove powers
        RemCompDeferred<ShadowkinDarkSwapPowerComponent>(ent);
        RemCompDeferred<ShadowkinDarkSwappedComponent>(ent);
        RemCompDeferred<ShadowkinRestPowerComponent>(ent);
        RemCompDeferred<ShadowkinTeleportPowerComponent>(ent);

        // Popup
        _popup.PopupEntity(Loc.GetString("shadowkin-blackeye"),ent, ent, PopupType.Large);

        if (!damage)
            return;

        // Stamina crit
        if (TryComp<StaminaComponent>(ent, out var stamina))
        {
            _stamina.TakeStaminaDamage(ent, stamina.CritThreshold, stamina, ent);
        }

        // Nearly crit with cellular damage
        // If already 5 damage off of crit, don't do anything
        if (!TryComp<DamageableComponent>(ent, out var damageable) ||
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
