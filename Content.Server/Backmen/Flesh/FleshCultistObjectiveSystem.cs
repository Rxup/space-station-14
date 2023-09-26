using Content.Server.Backmen.Objectives;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Shared.Objectives.Components;
using Robust.Shared.Players;

namespace Content.Server.Backmen.Flesh;

public sealed class FleshCultistObjectiveSystem : EntitySystem
{
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CreateFleshHeartConditionComponent, ObjectiveAssignedEvent>(OnCreateFleshHeartAssigned);
        SubscribeLocalEvent<CreateFleshHeartConditionComponent, ObjectiveGetProgressEvent>(OnFleshHeartProgress);

        SubscribeLocalEvent<FleshCultistSurvivalConditionComponent, ObjectiveAssignedEvent>(OnCreateFleshCultistSurvivalAssigned);
        SubscribeLocalEvent<FleshCultistSurvivalConditionComponent, ObjectiveGetProgressEvent>(OnFleshCultistSurvivalProgress);

        SubscribeLocalEvent<FleshCultistRoleComponent,MapInitEvent>(OnAssigned);
    }

    private void OnAssigned(EntityUid uid, FleshCultistRoleComponent component, MapInitEvent args)
    {
        if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
        {
            return;
        }

        if (!_mindSystem.TryGetSession(mindId, out var session))
        {
            return;
        }

        var isLeader = component.PrototypeId == "FleshCultistLeader";

        var query = EntityQueryEnumerator<FleshCultRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var cult, out var _))
        {
            if (isLeader)
            {
                GreetCultistLeader(session, cult.CultistsNames);
                continue;
            }

            GreetCultist(session, cult.CultistsNames);
        }
    }
    private void GreetCultist(ICommonSession session, List<string> cultistsNames)
    {
        _chatManager.DispatchServerMessage(session, Loc.GetString("flesh-cult-role-greeting"));
        _chatManager.DispatchServerMessage(session, Loc.GetString("flesh-cult-role-cult-members", ("cultMembers", string.Join(", ", cultistsNames))));
    }

    private void GreetCultistLeader(ICommonSession session, List<string> cultistsNames)
    {
        _chatManager.DispatchServerMessage(session, Loc.GetString("flesh-cult-role-greeting-leader"));
        _chatManager.DispatchServerMessage(session, Loc.GetString("flesh-cult-role-cult-members", ("cultMembers", string.Join(", ", cultistsNames))));
    }

    private void OnFleshCultistSurvivalProgress(EntityUid uid, FleshCultistSurvivalConditionComponent component, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = !_mindSystem.IsCharacterDeadIc(args.Mind) ? 1: 0;
    }

    private void OnCreateFleshCultistSurvivalAssigned(EntityUid uid, FleshCultistSurvivalConditionComponent component, ref ObjectiveAssignedEvent args)
    {

    }

    private void OnFleshHeartProgress(EntityUid uid, CreateFleshHeartConditionComponent component, ref ObjectiveGetProgressEvent args)
    {
        var fleshHeartFinale = false;

        foreach (var fleshHeartComp in EntityQuery<FleshHeartComponent>())
        {
            Logger.Info("Find flesh heart");
            if (!component.IsFleshHeartFinale(fleshHeartComp))
                continue;
            fleshHeartFinale = true;
            break;
        }

        args.Progress = fleshHeartFinale ? 1f : 0f;
    }

    private void OnCreateFleshHeartAssigned(EntityUid uid, CreateFleshHeartConditionComponent component, ref ObjectiveAssignedEvent args)
    {

    }
}
