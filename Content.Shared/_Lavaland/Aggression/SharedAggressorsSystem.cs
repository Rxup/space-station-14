using Content.Shared._Lavaland.Audio;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.Mobs;
using Content.Shared.NPC.Systems;
using JetBrains.Annotations;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._Lavaland.Aggression;

public abstract class SharedAggressorsSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    private EntityQuery<ActorComponent> _actorQuery;

    // TODO: make cooldowns for all individual aggressors that fall out of vision range

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AggressiveComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<AggressiveComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<AggressiveComponent, EntityTerminatingEvent>(OnDeleted);
        SubscribeLocalEvent<AggressiveComponent, DestructionEventArgs>(OnDestroyed);

        SubscribeLocalEvent<AggressorComponent, MobStateChangedEvent>(OnMobStateChange);
        _actorQuery = GetEntityQuery<ActorComponent>();
    }

    private void OnBeforeDamageChanged(Entity<AggressiveComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Origin == null && !_actorQuery.HasComp(ent))
            args.Cancelled = true;
    }

    private void OnDamageChanged(Entity<AggressiveComponent> ent, ref DamageChangedEvent args)
    {
        var aggro = args.Origin;

        if (aggro == null || !_actorQuery.HasComp(aggro))
            return;

        AddAggressor(ent, aggro.Value);
    }

    private void OnMobStateChange(Entity<AggressorComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            CleanAggressions(ent.AsNullable());
    }

    private void OnDeleted(Entity<AggressiveComponent> ent, ref EntityTerminatingEvent args)
    {
        RemoveAllAggressors(ent);
    }

    private void OnDestroyed(Entity<AggressiveComponent> ent, ref DestructionEventArgs args)
    {
        RemoveAllAggressors(ent);
    }

    #region api

    [PublicAPI]
    public HashSet<EntityUid>? GetAggressors(Entity<AggressiveComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return null;

        return ent.Comp.Aggressors;
    }

    [PublicAPI]
    public void RemoveAggressor(Entity<AggressiveComponent> ent, EntityUid aggressor)
    {
        _npcFaction.DeAggroEntity(ent.Owner, aggressor);
        ent.Comp.Aggressors.Remove(aggressor);
        RaiseLocalEvent(ent, new AggressorRemovedEvent(GetNetEntity(aggressor)));
    }

    [PublicAPI]
    public void RemoveAllAggressors(Entity<AggressiveComponent> ent)
    {
        var aggressors = ent.Comp.Aggressors;
        ent.Comp.Aggressors.Clear();
        foreach (var aggressor in aggressors)
        {
            _npcFaction.DeAggroEntity(ent.Owner, aggressor);
            RaiseLocalEvent(ent, new AggressorRemovedEvent(GetNetEntity(aggressor)));
        }
    }

    [PublicAPI]
    public void AddAggressor(Entity<AggressiveComponent> ent, EntityUid aggressor)
    {
        _npcFaction.AggroEntity(ent.Owner, aggressor);
        ent.Comp.Aggressors.Add(aggressor);

        var aggcomp = EnsureComp<AggressorComponent>(aggressor);
        RaiseLocalEvent(ent, new AggressorAddedEvent(GetNetEntity(aggressor)));

        aggcomp.Aggressives.Add(ent);

        if (!_net.IsServer ||
            !TryComp<BossMusicComponent>(ent, out var boss) ||
            !TryComp<AggressiveComponent>(ent, out var aggresive))
            return;

        var msg = new BossMusicStartupEvent(boss.SoundId);
        foreach (var aggress in aggresive.Aggressors)
        {
            if (!_actorQuery.TryComp(aggress, out var actor))
                continue;

            RaiseNetworkEvent(msg, actor.PlayerSession.Channel);
        }
    }

    [PublicAPI]
    public void CleanAggressions(Entity<AggressorComponent?> aggressor)
    {
        if (!Resolve(aggressor, ref aggressor.Comp))
            return;

        foreach (var aggrod in aggressor.Comp.Aggressives)
        {
            if (TryComp<AggressiveComponent>(aggrod, out var aggressors))
                RemoveAggressor((aggrod, aggressors), aggressor);
        }

        RemComp<AggressorComponent>(aggressor);
    }

    #endregion
}
