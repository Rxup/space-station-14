﻿using Content.Server.Antag;
using Content.Server.Backmen.Vampiric.Objective;
using Content.Server.Backmen.Vampiric.Role;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;

namespace Content.Server.Backmen.Vampiric.Rule;

public sealed class BloodsuckerRuleSystem : GameRuleSystem<BloodsuckerRuleComponent>
{
    [Dependency] private readonly BloodSuckerSystem _bloodSuckerSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodsuckerRuleComponent, AfterAntagEntitySelectedEvent>(AfterAntagSelected);
    }

    private void AfterAntagSelected(EntityUid uid, BloodsuckerRuleComponent component, AfterAntagEntitySelectedEvent args)
    {
        _bloodSuckerSystem.MakeVampire(args.EntityUid, true);
    }

    protected override void AppendRoundEndText(EntityUid uid,
        BloodsuckerRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent ev)
    {
        var skip = new List<EntityUid>();
        var query = AllEntityQuery<BloodsuckerRuleComponent>();
        while (query.MoveNext(out var vampRule))
        {
            if(vampRule.Elders.Count == 0)
                break;
            ev.AddLine(Loc.GetString("vampire-elder"));

            foreach (var player in vampRule.Elders)
            {
                skip.Add(player.Value);
                var role = CompOrNull<VampireRoleComponent>(player.Value);
                var count = role?.Converted ?? 0;
                var blood = role?.Drink ?? 0;
                var countGoal = 0;
                var bloodGoal = 0f;

                var mind = CompOrNull<MindComponent>(player.Value);
                if (_mindSystem.TryGetObjectiveComp<BloodsuckerDrinkConditionComponent>(player.Value, out var obj1, mind))
                {
                    bloodGoal = obj1.Goal;
                }
                if (_mindSystem.TryGetObjectiveComp<BloodsuckerConvertConditionComponent>(player.Value, out var obj2, mind))
                {
                    countGoal = obj2.Goal;
                }

                _mindSystem.TryGetSession(player.Value, out var session);
                var username = session?.Name;
                if (username != null)
                {
                    ev.AddLine(Loc.GetString("endgame-vamp-name-user", ("name", player.Key), ("username", username)));
                }
                else
                {
                    ev.AddLine(Loc.GetString("endgame-vamp-name", ("name", player.Key)));
                }
                ev.AddLine(Loc.GetString("endgame-vamp-conv",
                    ("count", count),
                    ("goal", countGoal)));
                ev.AddLine(Loc.GetString("endgame-vamp-drink",
                    ("count", blood),
                    ("goal", bloodGoal)));
            }

            ev.AddLine("");
        }

        var isAddLine = true;

        var q = EntityQueryEnumerator<MindComponent,VampireRoleComponent>();
        while (q.MoveNext(out var mindId,out var mind, out var role))
        {
            if (skip.Contains(mindId))
            {
                continue;
            }

            if (isAddLine)
            {
                ev.AddLine(Loc.GetString("vampire-bitten"));
                isAddLine = false;
            }
            var count = role?.Converted ?? 0;
            var blood = role?.Drink ?? 0;
            var countGoal = 0;
            var bloodGoal = 0f;

            if (_mindSystem.TryGetObjectiveComp<BloodsuckerDrinkConditionComponent>(mindId, out var obj1,mind))
            {
                bloodGoal = obj1.Goal;
            }
            if (_mindSystem.TryGetObjectiveComp<BloodsuckerConvertConditionComponent>(mindId, out var obj2,mind))
            {
                countGoal = obj2.Goal;
            }

            _mindSystem.TryGetSession(mindId, out var session);
            var username = session?.Name;
            if (username != null)
            {
                ev.AddLine(Loc.GetString("endgame-vamp-name-user", ("name", mind.CharacterName ?? "-"), ("username", username)));
            }
            else
            {
                ev.AddLine(Loc.GetString("endgame-vamp-name", ("name", mind.CharacterName ?? "-")));
            }
            ev.AddLine(Loc.GetString("endgame-vamp-conv",
                ("count", count),
                ("goal", countGoal)));
            ev.AddLine(Loc.GetString("endgame-vamp-drink",
                ("count", blood),
                ("goal", bloodGoal)));
        }
    }
}
