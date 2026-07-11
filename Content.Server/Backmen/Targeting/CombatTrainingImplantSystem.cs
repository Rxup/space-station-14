using Content.Shared.Backmen.Targeting;
using Content.Shared.Implants;

namespace Content.Server.Backmen.Targeting;

/// <summary>
/// Applies combat spread tier overrides from combat training implants.
/// </summary>
public sealed partial class CombatTrainingImplantSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CombatTrainingImplantComponent, ImplantImplantedEvent>(OnImplantImplanted);
        SubscribeLocalEvent<CombatTrainingImplantComponent, ImplantRemovedEvent>(OnImplantRemoved);
    }

    private void OnImplantImplanted(Entity<CombatTrainingImplantComponent> ent, ref ImplantImplantedEvent ev)
    {
        var oddsOverride = EnsureComp<CombatTargetOddsOverrideComponent>(ev.Implanted);
        oddsOverride.Odds = ent.Comp.Odds;
        oddsOverride.FromImplant = true;
        Dirty(ev.Implanted, oddsOverride);
    }

    private void OnImplantRemoved(Entity<CombatTrainingImplantComponent> ent, ref ImplantRemovedEvent ev)
    {
        if (!TryComp<CombatTargetOddsOverrideComponent>(ev.Implanted, out var oddsOverride) || !oddsOverride.FromImplant)
            return;

        RemComp<CombatTargetOddsOverrideComponent>(ev.Implanted);
    }
}
