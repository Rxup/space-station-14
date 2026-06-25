using Content.Server.Backmen.Psionics;
using Content.Server.Backmen.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffect;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Backmen.StationEvents.Events;

/// <summary>
/// Mutes everyone for a random amount of time.
/// </summary>
internal sealed partial class PsionicCatGotYourTongueRule : StationEventSystem<PsionicCatGotYourTongueRuleComponent>
{
    [Dependency] private MobStateSystem _mobStateSystem = default!;
    [Dependency] private StatusEffectsSystem _statusEffectsSystem = default!;
    [Dependency] private IRobustRandom _robustRandom = default!;
    [Dependency] private SharedAudioSystem _sharedAudioSystem = default!;
    [Dependency] private Shared.StatusEffectNew.StatusEffectsSystem _statusEffects = default!;



    protected override void Started(EntityUid uid, PsionicCatGotYourTongueRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        List<EntityUid> psionicList = new();

        var query = EntityQueryEnumerator<PotentialPsionicComponent, MobStateComponent>();
        while (query.MoveNext(out var psion, out _, out var mobStateComponent))
        {
            if (_mobStateSystem.IsAlive(psion, mobStateComponent) && !_statusEffects.HasEffectComp<PsionicInsulationComponent>(psion))
                psionicList.Add(psion);
        }

        foreach (var psion in psionicList)
        {
            var duration = _robustRandom.Next(component.MinDuration, component.MaxDuration);

            _statusEffectsSystem.TryAddStatusEffect(psion,
                "Muted",
                duration,
                false,
                "Muted");

            _sharedAudioSystem.PlayGlobal(component.Sound, Filter.Entities(psion), false);
        }
    }
}
