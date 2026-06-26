using Content.Server.Backmen.Abilities.Psionics;
using Content.Server.Backmen.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics.Components;
using Content.Shared.Backmen.Psionics.Glimmer;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Random;

namespace Content.Server.Backmen.StationEvents.Events;

internal sealed partial class NoosphericStormRule : StationEventSystem<NoosphericStormRuleComponent>
{
    [Dependency] private PsionicAbilitiesSystem _psionicAbilitiesSystem = default!;
    [Dependency] private MobStateSystem _mobStateSystem = default!;
    [Dependency] private GlimmerSystem _glimmerSystem = default!;
    [Dependency] private IRobustRandom _robustRandom = default!;
    [Dependency] private Shared.StatusEffectNew.StatusEffectsSystem _statusEffects = default!;


    protected override void Started(EntityUid uid, NoosphericStormRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        List<EntityUid> validList = new();

        var query = EntityQueryEnumerator<PotentialPsionicComponent>();
        while (query.MoveNext(out var potentialPsionic, out _))
        {
            if (_mobStateSystem.IsDead(potentialPsionic))
                continue;

            // Skip over those who are already psionic or those who are insulated.
            if (HasComp<PsionicComponent>(potentialPsionic) || _statusEffects.HasEffectComp<PsionicInsulationComponent>(potentialPsionic))
                continue;

            validList.Add(potentialPsionic);
        }

        // Give some targets psionic abilities.
        RobustRandom.Shuffle(validList);

        var toAwaken = RobustRandom.Next(1, component.MaxAwaken);

        foreach (var target in validList)
        {
            if (toAwaken-- == 0)
                break;

            _psionicAbilitiesSystem.AddPsionics(target);
        }

        // Increase glimmer.
        var baseGlimmerAdd = _robustRandom.Next(component.BaseGlimmerAddMin, component.BaseGlimmerAddMax);
        var glimmerSeverityMod = 1.66f;//1 + (component.GlimmerSeverityCoefficient * (GetSeverityModifier() - 1f));
        var glimmerAdded = (int) Math.Round(baseGlimmerAdd * glimmerSeverityMod);

        _glimmerSystem.Glimmer += glimmerAdded;
    }
}
