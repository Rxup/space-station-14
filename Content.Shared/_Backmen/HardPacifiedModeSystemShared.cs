using Content.Shared._Backmen.Psionics;
using Content.Shared._Backmen.Shipwrecked.Components;
using Content.Shared._Backmen.Species.Shadowkin.Components;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Interaction.Events;

namespace Content.Shared.Backmen;

public abstract class SharedHardPacifiedModeSystem : EntitySystem
{
    private EntityQuery<PacifiedComponent> _pacifyQuery;

    public override void Initialize()
    {
        base.Initialize();

        _pacifyQuery = GetEntityQuery<PacifiedComponent>();

        SubscribeLocalEvent<ShipwreckSurvivorComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<PsionicallyInvisibleComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<ShadowkinDarkSwappedComponent, AttackAttemptEvent>(OnAttackAttempt);
    }

    private void OnAttackAttempt<T>(Entity<T> ent, ref AttackAttemptEvent args) where T : Component
    {
        if(_pacifyQuery.HasComp(ent))
            args.Cancel();
    }
}
