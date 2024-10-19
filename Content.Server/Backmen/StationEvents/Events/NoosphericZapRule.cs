using Content.Server.Backmen.Psionics;
using Content.Server.Backmen.StationEvents.Components;
using Content.Server.Popups;
using Content.Server.StationEvents.Events;
using Content.Server.Stunnable;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffect;

namespace Content.Server.Backmen.StationEvents.Events;

/// <summary>
/// Zaps everyone, rolling psionics and disorienting them
/// </summary>
internal sealed class NoosphericZapRule : StationEventSystem<NoosphericZapRuleComponent>
{
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly StunSystem _stunSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly PsionicsSystem _psionicsSystem = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffectsSystem = default!;

    protected override void Started(EntityUid uid, NoosphericZapRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var query = EntityQueryEnumerator<PotentialPsionicComponent, MobStateComponent>();

        while (query.MoveNext(out var psion, out var potentialPsionicComponent, out var mobStateComponent))
        {
            if (!_mobStateSystem.IsAlive(psion, mobStateComponent) || HasComp<PsionicInsulationComponent>(psion))
                continue;

            _stunSystem.TryParalyze(psion, TimeSpan.FromSeconds(5), false);
            _statusEffectsSystem.TryAddStatusEffect(psion, "Stutter", TimeSpan.FromSeconds(10), false, "StutteringAccent");

            if (HasComp<PsionicComponent>(psion))
                _popupSystem.PopupEntity(Loc.GetString("noospheric-zap-seize"), psion, psion, Shared.Popups.PopupType.LargeCaution);
            else
            {
                if (potentialPsionicComponent.Rerolled)
                {
                    potentialPsionicComponent.Rerolled = false;
                    _popupSystem.PopupEntity(Loc.GetString("noospheric-zap-seize-potential-regained"), psion, psion, Shared.Popups.PopupType.LargeCaution);
                }
                else
                {
                    _psionicsSystem.RollPsionics((psion, potentialPsionicComponent), multiplier: 0.25f);
                    _popupSystem.PopupEntity(Loc.GetString("noospheric-zap-seize"), psion, psion, Shared.Popups.PopupType.LargeCaution);
                }
            }
        }
    }
}
