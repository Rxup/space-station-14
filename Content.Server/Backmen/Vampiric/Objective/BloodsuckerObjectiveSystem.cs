using Content.Server.Backmen.Vampiric.Role;
using Content.Shared.Backmen.Vampiric;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Vampiric.Objective;

public sealed class BloodsuckerObjectiveSystem : EntitySystem
{
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedRoleSystem _roleSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodsuckerConvertConditionComponent, ObjectiveGetProgressEvent>(OnGetConvertProgress);
        SubscribeLocalEvent<BloodsuckerDrinkConditionComponent, ObjectiveGetProgressEvent>(OnGetDrinkProgress);
        SubscribeLocalEvent<BloodsuckerConvertConditionComponent, ObjectiveAssignedEvent>(OnConvertAssigned);
        SubscribeLocalEvent<BloodsuckerConvertConditionComponent, ObjectiveAfterAssignEvent>(OnConvertAfterAssigned);
        SubscribeLocalEvent<BloodsuckerDrinkConditionComponent, ObjectiveAssignedEvent>(OnDrinkAssigned);
        SubscribeLocalEvent<BloodsuckerDrinkConditionComponent, ObjectiveAfterAssignEvent>(OnDrinkAfterAssigned);
    }

    private void OnDrinkAfterAssigned(Entity<BloodsuckerDrinkConditionComponent> condition, ref ObjectiveAfterAssignEvent args)
    {
        _metaData.SetEntityName(condition.Owner, Loc.GetString(condition.Comp.ObjectiveText, ("goal", condition.Comp.Goal)), args.Meta);
        _metaData.SetEntityDescription(condition.Owner, Loc.GetString(condition.Comp.DescriptionText, ("goal", condition.Comp.Goal)), args.Meta);
    }

    private void OnConvertAfterAssigned(Entity<BloodsuckerConvertConditionComponent> condition, ref ObjectiveAfterAssignEvent args)
    {
        _metaData.SetEntityName(condition.Owner, Loc.GetString(condition.Comp.ObjectiveText, ("goal", condition.Comp.Goal)), args.Meta);
        _metaData.SetEntityDescription(condition.Owner, Loc.GetString(condition.Comp.DescriptionText, ("goal", condition.Comp.Goal)), args.Meta);
    }

    private void OnConvertAssigned(Entity<BloodsuckerConvertConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        ent.Comp.Goal = _random.Next(
            1,
            Math.Max(1, // min 1 of 1
                Math.Min(
                    ent.Comp.MaxGoal, // 5
                    (int)Math.Ceiling(Math.Max(_playerManager.PlayerCount, 1f) / ent.Comp.PerPlayers) // per players with max
                    )
                )
            );
    }

    private void OnGetConvertProgress(Entity<BloodsuckerConvertConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        if (!_roleSystem.MindHasRole<VampireRoleComponent>(args.MindId, out var vmp))
        {
            args.Progress = 0;
            return;
        }

        args.Progress = vmp.Value.Comp2.Converted / ent.Comp.Goal;
    }

    private void OnDrinkAssigned(Entity<BloodsuckerDrinkConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        ent.Comp.Goal = _random.Next(
            ent.Comp.MinGoal,
            Math.Max(ent.Comp.MinGoal + 1, // min 1 of 1
                ent.Comp.MaxGoal
            )
        );
    }

    private void OnGetDrinkProgress(Entity<BloodsuckerDrinkConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        if (!_roleSystem.MindHasRole<VampireRoleComponent>(args.MindId, out var role))
        {
            args.Progress = 0;
            return;
        }
        args.Progress = role.Value.Comp2.Drink / ent.Comp.Goal;
    }
}
