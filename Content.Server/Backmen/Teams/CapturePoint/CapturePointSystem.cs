using System.Linq;
using Content.Server.DeviceLinking.Systems;
using Content.Server.DeviceNetwork.Components;
using Content.Server.DeviceNetwork.Systems;
using Content.Shared.Backmen.Teams;
using Content.Shared.Backmen.Teams.CapturePoint;
using Content.Shared.Backmen.Teams.CapturePoint.Components;
using Content.Shared.Backmen.Teams.Components;
using Content.Shared.DeviceNetwork;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.Server.Backmen.Teams.CapturePoint;

public sealed class CapturePointSystem : SharedCapturePointSystem
{
    [Dependency] private readonly DeviceLinkSystem _deviceLink = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly AppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly DeviceNetworkSystem _deviceNetSystem = default!;
    private EntityQuery<ActorComponent> _actorQuery;
    private EntityQuery<TdmMemberComponent> _teamQuery;
    private EntityQuery<MobStateComponent> _mobStateQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BkmCapturePointComponent, StartCollideEvent>(OnCaptureEnter);
        SubscribeLocalEvent<BkmCapturePointComponent, EndCollideEvent>(OnCaptureExit);
        SubscribeLocalEvent<BkmCapturePointComponent, BkmCapturePointChangeEvent>(OnCaptureChange);
        SubscribeLocalEvent<BkmCapturePointComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BkmCapturePointComponent, BkmCaptureChangeStatusEvent>(OnChangeStatus);
        SubscribeLocalEvent<BkmCapturePointComponent,BkmCaptureDoneEvent>(OnCaptureDone);

        _actorQuery = GetEntityQuery<ActorComponent>();
        _teamQuery = GetEntityQuery<TdmMemberComponent>();
        _mobStateQuery = GetEntityQuery<MobStateComponent>();
    }

    private void OnCaptureDone(Entity<BkmCapturePointComponent> ent, ref BkmCaptureDoneEvent args)
    {
        _popupSystem.PopupEntity(Loc.GetString("bkm-ctp-captured-"+args.Team), ent, PopupType.LargeCaution);
        _deviceLink.EnsureSourcePorts(ent, ent.Comp.OutputPortTeamA, ent.Comp.OutputPortTeamB);

        if (args.Team == StationTeamMarker.TeamA)
        {
            _deviceLink.SendSignal(ent, ent.Comp.OutputPortTeamA, true);
            _deviceLink.SendSignal(ent, ent.Comp.OutputPortTeamB, false);
        }
        else if (args.Team == StationTeamMarker.TeamB)
        {
            _deviceLink.SendSignal(ent, ent.Comp.OutputPortTeamA, false);
            _deviceLink.SendSignal(ent, ent.Comp.OutputPortTeamB, true);
        }
        else
        {
            _deviceLink.SendSignal(ent, ent.Comp.OutputPortTeamA, false);
            _deviceLink.SendSignal(ent, ent.Comp.OutputPortTeamB, false);
        }
    }

    private void OnChangeStatus(Entity<BkmCapturePointComponent> ent, ref BkmCaptureChangeStatusEvent args)
    {
        if (args.Team == StationTeamMarker.TeamA && ent.Comp.Team == StationTeamMarker.Neutral && ent.Comp.CaptureCurrent <= 0)
        {
            // convert to teamA
            ent.Comp.Team = args.Team;
            ent.Comp.CaptureCurrent = ent.Comp.CaptureTick;
            Dirty(ent);

        }

        if ((ent.Comp.Team == StationTeamMarker.TeamA || ent.Comp.Team == StationTeamMarker.TeamB) && ent.Comp.CaptureCurrent <= 0)
        {
            // convert to neutral
            ent.Comp.Team = StationTeamMarker.Neutral;
            ent.Comp.CaptureCurrent = ent.Comp.CaptureMax - ent.Comp.CaptureTick;
            _deviceLink.SendSignal(ent, ent.Comp.OutputPortTeamA, false);
            _deviceLink.SendSignal(ent, ent.Comp.OutputPortTeamB, false);
            Dirty(ent);
        }

        if (args.Team == StationTeamMarker.TeamB && ent.Comp.Team == StationTeamMarker.Neutral && ent.Comp.CaptureCurrent <= 0)
        {
            // convert to teamB
            ent.Comp.Team = args.Team;
            ent.Comp.CaptureCurrent = ent.Comp.CaptureTick;
            Dirty(ent);
        }

        if (args.Team == ent.Comp.Team && ent.Comp.CaptureCurrent >= ent.Comp.CaptureMax)
        {
            RaiseLocalEvent(ent, new BkmCaptureDoneEvent(args.Team), true);
        }
    }

    private void OnStartup(Entity<BkmCapturePointComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.CaptureCurrent = ent.Comp.CaptureMax;
        var appearance = EnsureComp<AppearanceComponent>(ent);
        UpdateAppearance(ent, true, appearance);
        Dirty(ent);
    }

    private void UpdateAppearance(Entity<BkmCapturePointComponent> ent, bool resetMoving = false, AppearanceComponent? component = null)
    {
        if (!Resolve(ent, ref component, false))
            return;

        if (resetMoving)
        {
            _appearanceSystem.SetData(ent, BkmCPTVisualState.TeamAToNeutral, false, component);
            _appearanceSystem.SetData(ent, BkmCPTVisualState.TeamBToNeutral, false, component);
            _appearanceSystem.SetData(ent, BkmCPTVisualState.TeamNeutralToA, false, component);
            _appearanceSystem.SetData(ent, BkmCPTVisualState.TeamNeutralToB, false, component);
        }

        switch (ent.Comp.Team)
        {
            case StationTeamMarker.TeamA:
                _appearanceSystem.SetData(ent, BkmCPTVisualState.TeamNeutral, false, component);
                _appearanceSystem.SetData(ent, BkmCPTVisualState.TeamA, true, component);
                _appearanceSystem.SetData(ent, BkmCPTVisualState.TeamB, false, component);
                break;
            case StationTeamMarker.TeamB:
                _appearanceSystem.SetData(ent, BkmCPTVisualState.TeamNeutral, false, component);
                _appearanceSystem.SetData(ent, BkmCPTVisualState.TeamA, false, component);
                _appearanceSystem.SetData(ent, BkmCPTVisualState.TeamB, true, component);
                break;
            default:
                _appearanceSystem.SetData(ent, BkmCPTVisualState.TeamNeutral, true, component);
                _appearanceSystem.SetData(ent, BkmCPTVisualState.TeamA, false, component);
                _appearanceSystem.SetData(ent, BkmCPTVisualState.TeamB, false, component);
                break;
        }
    }

    private void UpdateAppearanceMoving(Entity<BkmCapturePointComponent> ent, StationTeamMarker from, StationTeamMarker to, AppearanceComponent? component = null)
    {
        if (!Resolve(ent, ref component, false))
            return;

        if (from == to)
        {
            _appearanceSystem.SetData(ent, BkmCPTVisualState.TeamAToNeutral, false, component);
            _appearanceSystem.SetData(ent, BkmCPTVisualState.TeamBToNeutral, false, component);
            _appearanceSystem.SetData(ent, BkmCPTVisualState.TeamNeutralToA, false, component);
            _appearanceSystem.SetData(ent, BkmCPTVisualState.TeamNeutralToB, false, component);
            return;
        }
        _appearanceSystem.SetData(ent, BkmCPTVisualState.TeamAToNeutral, from == StationTeamMarker.TeamA && to == StationTeamMarker.Neutral, component);
        _appearanceSystem.SetData(ent, BkmCPTVisualState.TeamBToNeutral, from == StationTeamMarker.TeamB && to == StationTeamMarker.Neutral, component);
        _appearanceSystem.SetData(ent, BkmCPTVisualState.TeamNeutralToA, from == StationTeamMarker.Neutral && to == StationTeamMarker.TeamA, component);
        _appearanceSystem.SetData(ent, BkmCPTVisualState.TeamNeutralToB, from == StationTeamMarker.Neutral && to == StationTeamMarker.TeamB, component);
    }

    private void OnCaptureChange(Entity<BkmCapturePointComponent> ent, ref BkmCapturePointChangeEvent args)
    {
        if (args.CaptureInfo.Count == 0 && ent.Comp.Team == StationTeamMarker.Neutral)
        {
            // if capture point is empty do rollback
            ent.Comp.CaptureCurrent = FixedPoint2.Clamp(ent.Comp.CaptureCurrent + ent.Comp.CaptureTick, 0, ent.Comp.CaptureMax);
            Dirty(ent);
            UpdateAppearanceMoving(ent, ent.Comp.Team, ent.Comp.Team);
            return;
        }

        if (args.CaptureInfo.Keys.Count == 1)
        {
            var team = args.CaptureInfo.Keys.First();
            if (ent.Comp.Team == StationTeamMarker.Neutral && team != StationTeamMarker.Neutral)
            {
                ent.Comp.CaptureCurrent = FixedPoint2.Clamp(ent.Comp.CaptureCurrent - ent.Comp.CaptureTick, 0, ent.Comp.CaptureMax);
            }
            else
            {
                var add = ent.Comp.Team == team;
                ent.Comp.CaptureCurrent = FixedPoint2.Clamp(add ? ent.Comp.CaptureCurrent + ent.Comp.CaptureTick : ent.Comp.CaptureCurrent - ent.Comp.CaptureTick, 0, ent.Comp.CaptureMax);
            }

            Dirty(ent);
            UpdateAppearanceMoving(ent, ent.Comp.Team, team);
            if (ent.Comp.CaptureCurrent == 0 || ent.Comp.CaptureMax == ent.Comp.CaptureCurrent)
            {
                // raise event to change owner of point
                RaiseLocalEvent(ent, new BkmCaptureChangeStatusEvent(team));
            }
            return;
        }
        // dothing? two team on one point
    }

    private void OnCaptureExit(Entity<BkmCapturePointComponent> ent, ref EndCollideEvent args)
    {
        ent.Comp.CapturedEntities.Remove(args.OtherEntity);
    }

    private void OnCaptureEnter(Entity<BkmCapturePointComponent> ent, ref StartCollideEvent args)
    {
        var plr = args.OtherEntity;
        if (!_actorQuery.HasComp(plr) || !_teamQuery.HasComp(plr) || !_mobStateQuery.HasComp(plr))
        {
            return;
        }
        ent.Comp.CapturedEntities.Add(plr);
    }

    public override void Update(float frameTime)
    {
        var q = EntityQueryEnumerator<BkmCapturePointComponent, MetaDataComponent>();

        while (q.MoveNext(out var capturePoint, out var ctp, out var md))
        {
            if(Paused(capturePoint,md))
                continue;

            ctp.Acc += frameTime;

            if(ctp.Acc <= TicksPerSecond)
                continue;

            ctp.Acc -= TicksPerSecond;

            var teamValue = new Dictionary<StationTeamMarker, int>();

            var qEntity = EntityQueryEnumerator<ActorComponent, TdmMemberComponent, MobStateComponent>();
            while (qEntity.MoveNext(out var owner, out _, out var team, out var mobStateComponent))
            {
                if(!ctp.CapturedEntities.Contains(owner))
                    continue;

                if(!_mobStateSystem.IsAlive(owner, mobStateComponent))
                    continue;

                teamValue.TryAdd(team.Team, 0);
                teamValue[team.Team]++;
            }

            if(!(teamValue.Count > 0 || (ctp.CaptureCurrent > 0 && ctp.CaptureCurrent < ctp.CaptureMax)))
               continue; // point is not running do nothing

            if(teamValue.Keys.Count == 1 && teamValue.Keys.First() == ctp.Team && ctp.CaptureCurrent == ctp.CaptureMax)
                continue;

            var ev = new BkmCapturePointChangeEvent(teamValue);
            RaiseLocalEvent(capturePoint, ev, true);
        }

        base.Update(frameTime);
    }
}
