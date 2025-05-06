using System.Linq;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Shared.Actions;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Server.NPC.Systems;

public sealed class NPCUseActionOnTargetSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IRobustRandom _random = default!; // Backmen

    private readonly Dictionary<EntityUid, Dictionary<EntProtoId<EntityWorldTargetActionComponent>, TimeSpan>> _lastUsed = new(); // Backmen

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NPCUseActionOnTargetComponent, MapInitEvent>(OnMapInit);
    }

    // Backmen-EDIT-Start
    private void OnMapInit(Entity<NPCUseActionOnTargetComponent> ent, ref MapInitEvent args)
    {
        _lastUsed[ent] = new Dictionary<EntProtoId<EntityWorldTargetActionComponent>, TimeSpan>();

        ent.Comp.ActionEntities = new List<EntityUid>();

        foreach (var actionProtoId in ent.Comp.ActionId)
        {
            var actionEnt = _actions.AddAction(ent.Owner, actionProtoId);
            if (actionEnt == null || !actionEnt.HasValue)
                continue;

            ent.Comp.ActionEntities.Add(actionEnt.Value);
        }
    }

    public bool TryUseAction(EntityUid user, EntityUid target, List<EntityUid> actionEntities) // Backmen-EDIT | Renamed to TryUseAction
    {
        if (!TryComp<NPCUseActionOnTargetComponent>(user, out var comp))
            return false;

        if (_timing.CurTime < comp.LastUseAnyActionTime + comp.UseActionDelay)
            return false;

        if (!actionEntities.Any())
            return false;

        var actionEnt = _random.Pick(actionEntities);

        if (!TryComp<EntityWorldTargetActionComponent>(actionEnt, out var action))
            return false;

        var actionUseDelay = action.UseDelay;

        if (!_lastUsed.ContainsKey(user))
            _lastUsed[user] = new Dictionary<EntProtoId<EntityWorldTargetActionComponent>, TimeSpan>();

        EntProtoId<EntityWorldTargetActionComponent> actionProtoId = new EntProtoId<EntityWorldTargetActionComponent>(Name(actionEnt));
        if (_lastUsed[user].TryGetValue(actionProtoId, out var lastUsedTime))
            if (_timing.CurTime < lastUsedTime + comp.UseActionDelay + actionUseDelay)
                return false;

        _lastUsed[user][actionProtoId] = _timing.CurTime;

        if (!_actions.ValidAction(action))
            return false;

        if (action.Event != null)
            action.Event.Coords = Transform(target).Coordinates;

        _actions.PerformAction(user,
            null,
            actionEnt,
            action,
            action.BaseEvent,
            _timing.CurTime,
            false);

        comp.LastUseAnyActionTime = _timing.CurTime;

        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Tries to use the attack on the current target.
        var query = EntityQueryEnumerator<NPCUseActionOnTargetComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out var comp, out var htn))
        {
            if (!htn.Blackboard.TryGetValue<EntityUid>(comp.TargetKey, out var target, EntityManager))
                continue;

            UpdateActions(uid, comp);
            TryUseAction(uid, target, comp.ActionEntities);
        }
    }

    private void UpdateActions(EntityUid uid, NPCUseActionOnTargetComponent comp)
    {
        var existingActions = new HashSet<string>(comp.ActionEntities.Select(e => MetaData(e).EntityPrototype?.ID ?? string.Empty));

        foreach (var actionProtoId in comp.ActionId)
        {
            if (existingActions.Contains(actionProtoId))
                continue;

            var actionEnt = _actions.AddAction(uid, actionProtoId);
            if (actionEnt == null || !actionEnt.HasValue)
                continue;

            comp.ActionEntities.Add(actionEnt.Value);
        }
    // Backmen-EDIT-End
    }
}
