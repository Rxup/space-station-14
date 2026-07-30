using System.Numerics;
using Content.Server.Salvage.Expeditions;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared.Chat;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Salvage.Expeditions;
using Content.Shared.Shuttles.Components;
using Content.Shared.Localizations;
using Content.Shared.Station.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server.Salvage;

public sealed partial class SalvageSystem
{
    /*
     * Handles actively running a salvage expedition.
     */

    [Dependency] private MobStateSystem _mobState = default!;

    private void InitializeRunner()
    {
        SubscribeLocalEvent<FTLRequestEvent>(OnFTLRequest);
        SubscribeLocalEvent<FTLStartedEvent>(OnFTLStarted);
        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
        SubscribeLocalEvent<ConsoleFTLAttemptEvent>(OnConsoleFTLAttempt);
    }

    private void OnConsoleFTLAttempt(ref ConsoleFTLAttemptEvent ev)
    {
        if (!TryComp(ev.Uid, out TransformComponent? xform) ||
            !TryComp<SalvageExpeditionComponent>(xform.MapUid, out var salvage))
        {
            return;
        }

        // TODO: This is terrible but need bluespace harnesses or something.
        var query = EntityQueryEnumerator<HumanoidProfileComponent, MobStateComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out _, out var mobState, out var mobXform))
        {
            if (mobXform.MapUid != xform.MapUid)
                continue;

            // Don't count unidentified humans (loot) or anyone you murdered so you can still maroon them once dead.
            if (_mobState.IsDead(uid, mobState))
                continue;

            // Okay they're on salvage, so are they on the shuttle.
            if (mobXform.GridUid != ev.Uid)
            {
                ev.Cancelled = true;
                ev.Reason = Loc.GetString("salvage-expedition-not-all-present");
                return;
            }
        }
    }

    /// <summary>
    /// Announces status updates to salvage crewmembers on the state of the expedition.
    /// </summary>
    private void Announce(EntityUid mapUid, string text)
    {
        var mapId = Comp<MapComponent>(mapUid).MapId;

        // I love TComms and chat!!!
        _chat.ChatMessageToManyFiltered(
            Filter.BroadcastMap(mapId),
            ChatChannel.Radio,
            text,
            text,
            _mapSystem.GetMapOrInvalid(mapId),
            false,
            true,
            null);
    }

    private void OnFTLRequest(ref FTLRequestEvent ev)
    {
        if (!HasComp<SalvageExpeditionComponent>(ev.MapUid) ||
            !TryComp<FTLDestinationComponent>(ev.MapUid, out var dest))
        {
            return;
        }

        // Only one shuttle can occupy an expedition.
        dest.Enabled = false;
        _shuttleConsoles.RefreshShuttleConsoles();
    }

    private void OnFTLCompleted(ref FTLCompletedEvent args)
    {
        if (!TryComp<SalvageExpeditionComponent>(args.MapUid, out var component))
            return;

        // Someone FTLd there so start announcement
        if (component.Stage != ExpeditionStage.Added)
            return;

        Announce(args.MapUid, Loc.GetString("salvage-expedition-announcement-countdown-minutes", ("duration", (component.EndTime - _timing.CurTime).Minutes)));

        var directionLocalization = ContentLocalizationManager.FormatDirection(component.DungeonLocation.GetDir()).ToLower();

        if (component.DungeonLocation != Vector2.Zero)
            Announce(args.MapUid, Loc.GetString("salvage-expedition-announcement-dungeon", ("direction", directionLocalization)));

        component.Stage = ExpeditionStage.Running;
        Dirty(args.MapUid, component);
    }

    private void OnFTLStarted(ref FTLStartedEvent ev)
    {
        if (!TryComp<SalvageExpeditionComponent>(ev.FromMapUid, out var expedition) ||
            !TryComp<SalvageExpeditionDataComponent>(expedition.Station, out var station))
        {
            return;
        }

        // Check if any shuttles remain.
        var query = EntityQueryEnumerator<ShuttleComponent, TransformComponent>();

        while (query.MoveNext(out _, out var xform))
        {
            if (xform.MapUid == ev.FromMapUid)
                return;
        }

        // Last shuttle has left so finish the mission.
        QueueDel(ev.FromMapUid.Value);
    }

    // Runs the expedition
    private void UpdateRunner()
    {
        // Generic missions
        var query = EntityQueryEnumerator<SalvageExpeditionComponent>();

        // Run the basic mission timers (e.g. announcements, auto-FTL, completion, etc)
        while (query.MoveNext(out var uid, out var comp))
        {
            var remaining = comp.EndTime - _timing.CurTime;
            var audioLength = _audio.GetAudioLength(comp.SelectedSong);

            if (comp.Stage < ExpeditionStage.FinalCountdown && remaining < TimeSpan.FromSeconds(45))
            {
                comp.Stage = ExpeditionStage.FinalCountdown;
                Dirty(uid, comp);
                Announce(uid, Loc.GetString("salvage-expedition-announcement-countdown-seconds", ("duration", TimeSpan.FromSeconds(45).Seconds)));
            }
            else if (comp.Stream == null && remaining < audioLength)
            {
                var audio = _audio.PlayPvs(comp.Sound, uid);
                comp.Stream = audio?.Entity;
                _audio.SetMapAudio(audio);
                comp.Stage = ExpeditionStage.MusicCountdown;
                Dirty(uid, comp);
                Announce(uid, Loc.GetString("salvage-expedition-announcement-countdown-minutes", ("duration", audioLength.Minutes)));
            }
            else if (comp.Stage < ExpeditionStage.Countdown && remaining < TimeSpan.FromMinutes(4))
            {
                comp.Stage = ExpeditionStage.Countdown;
                Dirty(uid, comp);
                Announce(uid, Loc.GetString("salvage-expedition-announcement-countdown-minutes", ("duration", TimeSpan.FromMinutes(5).Minutes)));
            }
            // Auto-FTL out any shuttles
            else if (remaining < TimeSpan.FromSeconds(_shuttle.DefaultStartupTime) + TimeSpan.FromSeconds(0.5))
            {
                var ftlTime = (float)remaining.TotalSeconds;

                if (remaining < TimeSpan.FromSeconds(_shuttle.DefaultStartupTime))
                {
                    ftlTime = MathF.Max(0, (float)remaining.TotalSeconds - 0.5f);
                }

                ftlTime = MathF.Min(ftlTime, _shuttle.DefaultStartupTime);
                var shuttleQuery = AllEntityQuery<ShuttleComponent, TransformComponent>();

                // start-backmen: salvage-return-station
                // HashSet.Grids order is undefined — first member can be cargo itself or a
                // grid parked on CentCom (Backmen CentCom cargo / mis-linked members), so
                // auto-FTL must target the largest non-CentCom station grid like Arrivals does.
                if (GetExpeditionReturnGrid(comp.Station) is { } targetGrid)
                {
                    while (shuttleQuery.MoveNext(out var shuttleUid, out var shuttle, out var shuttleXform))
                    {
                        if (shuttleXform.MapUid != uid || HasComp<FTLComponent>(shuttleUid))
                            continue;

                        _shuttle.FTLToDock(shuttleUid, shuttle, targetGrid, ftlTime);
                    }
                }
                // end-backmen: salvage-return-station
            }

            if (remaining < TimeSpan.Zero)
            {
                QueueDel(uid);
            }
        }
    }

    // start-backmen: salvage-return-station
    /// <summary>
    /// Grid to auto-dock to when an expedition ends. Largest station grid that is not
    /// CentCom and not another shuttle (cargo is a station member but must not be the target).
    /// </summary>
    private EntityUid? GetExpeditionReturnGrid(EntityUid station)
    {
        if (!TryComp<StationDataComponent>(station, out var data))
            return null;

        TryComp<StationCentcommComponent>(station, out var centcomm);
        var centComGrid = centcomm?.Entity;
        var centComMap = centcomm?.MapEntity;

        // True only for the CentCom station (its member grids live on the CentCom map).
        // Regular stations also have StationCentcommComponent.Entity set to CentCom — ignore that.
        var stationIsCentCom = false;
        if (centComMap != null)
        {
            foreach (var gridUid in data.Grids)
            {
                if (HasComp<ShuttleComponent>(gridUid))
                    continue;

                if (TryComp(gridUid, out TransformComponent? xform) && xform.MapUid == centComMap)
                {
                    stationIsCentCom = true;
                    break;
                }
            }
        }

        EntityUid? largest = null;
        var largestSize = 0f;

        foreach (var gridUid in data.Grids)
        {
            if (!stationIsCentCom)
            {
                if (centComGrid != null && gridUid == centComGrid)
                    continue;

                if (centComMap != null
                    && TryComp(gridUid, out TransformComponent? gridXform)
                    && gridXform.MapUid == centComMap)
                {
                    continue;
                }
            }

            // Cargo / other shuttles are station members but must not be the FTL target.
            if (HasComp<ShuttleComponent>(gridUid))
                continue;

            if (!_gridQuery.TryComp(gridUid, out var grid))
                continue;

            var size = grid.LocalAABB.Size.LengthSquared();
            if (size < largestSize)
                continue;

            largestSize = size;
            largest = gridUid;
        }

        return largest ?? _station.GetLargestGrid((station, data));
    }
    // end-backmen: salvage-return-station
}
