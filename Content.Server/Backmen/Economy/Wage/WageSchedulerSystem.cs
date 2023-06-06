using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;

namespace Content.Server.Backmen.Economy.Wage;

/// <summary>
///     Simple GameRule that will do a free-for-all death match.
///     Kill everybody else to win.
/// </summary>
[RegisterComponent, Access(typeof(WageSchedulerSystem))]
public sealed class WageSchedulerRuleComponent : Component
{

}


public sealed class WageSchedulerSystem : GameRuleSystem<WageSchedulerRuleComponent>
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    //public override string Prototype => "WageScheduler";
    //[Dependency] private readonly WageManagerSystem _wageManagerSystem = default!;
    private const float MinimumTimeUntilFirstWage = 900;
    private const float WageInterval = 1800;
    [ViewVariables(VVAccess.ReadWrite)]
    private float _timeUntilNextWage = MinimumTimeUntilFirstWage;

    protected override void Started(EntityUid uid, WageSchedulerRuleComponent component, GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        _chatManager.DispatchServerAnnouncement(Loc.GetString("rule-wage-announcement"));
    }
    protected override void Ended(EntityUid uid, WageSchedulerRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);
        _timeUntilNextWage = MinimumTimeUntilFirstWage;
    }

    protected override void ActiveTick(EntityUid uid, WageSchedulerRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);
/*
        if (!_wageManagerSystem.WagesEnabled)
            return;
        if (_timeUntilNextWage > 0)
        {
            _timeUntilNextWage -= frameTime;
            return;
        }
        _wageManagerSystem.Payday();
        _timeUntilNextWage = WageInterval;
        */
    }
}
