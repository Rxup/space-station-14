﻿using System.Linq;
using Content.Server.AlertLevel;
using Content.Server.Backmen.SpecForces;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Nuke;
using Content.Server.RoundEnd;
using Content.Server.Station.Systems;
using Content.Shared.Blob;
using Robust.Shared.Audio;

namespace Content.Server.GameTicking.Rules;

public sealed class BlobRuleSystem : GameRuleSystem<BlobRuleComponent>
{
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly NukeCodePaperSystem _nukeCode = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;

    private ISawmill _sawmill = default!;
    private static readonly SoundPathSpecifier BlobDetectAudio = new SoundPathSpecifier("/Audio/Corvax/Adminbuse/Outbreak5.ogg");

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("preset");

        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var blobFactoryQuery = EntityQueryEnumerator<BlobRuleComponent>();
        while (blobFactoryQuery.MoveNext(out var blobRuleUid, out var blobRuleComp))
        {
            var blobCoreQuery = EntityQueryEnumerator<BlobCoreComponent>();
            while (blobCoreQuery.MoveNext(out var ent, out var comp))
            {
                if (TerminatingOrDeleted(ent))
                {
                    continue;
                }

                if (comp.BlobTiles.Count >= 50)
                {
                    if (_roundEndSystem.ExpectedCountdownEnd != null)
                    {
                        _roundEndSystem.CancelRoundEndCountdown(checkCooldown: false);
                        _chatSystem.DispatchGlobalAnnouncement(Loc.GetString("blob-alert-recall-shuttle"),
                            Loc.GetString("Station"),
                            false,
                            null,
                            Color.Red);
                    }
                }

                var stationUid = _stationSystem.GetOwningStation(ent);
                switch (blobRuleComp.Stage)
                {
                    case BlobStage.Default when comp.BlobTiles.Count < 30:
                        continue;
                    case BlobStage.Default:
                        _chatSystem.DispatchGlobalAnnouncement(Loc.GetString("blob-alert-detect"),
                            Loc.GetString("Station"), true, BlobDetectAudio, Color.Red);
                        EntitySystem.Get<AlertLevelSystem>()
                            .SetLevel(stationUid!.Value, "sigma", true, true, true, false);
                        blobRuleComp.Stage = BlobStage.Begin;
                        break;
                    case BlobStage.Begin:
                    {
                        if (comp.BlobTiles.Count >= 500)
                        {
                            _chatSystem.DispatchGlobalAnnouncement(Loc.GetString("blob-alert-critical"),
                                Loc.GetString("Station"),
                                true,
                                blobRuleComp.AlertAudio,
                                Color.Red);
                            if (stationUid != null)
                                _nukeCode.SendNukeCodes(stationUid.Value);
                            blobRuleComp.Stage = BlobStage.Critical;
                            EntitySystem.Get<AlertLevelSystem>()
                                .SetLevel(stationUid!.Value, "gamma", true, true, true, false);

                            EntityManager.System<SpecForcesSystem>().CallOps(SpecForcesType.RXBZZ, "ДСО");
                        }

                        break;
                    }
                    case BlobStage.Critical:
                    {
                        if (comp.BlobTiles.Count >= 900)
                        {
                            comp.Points = 99999;
                            _roundEndSystem.EndRound();
                        }

                        break;
                    }
                }
            }
        }
    }

    private void OnRoundEndText(RoundEndTextAppendEvent ev)
    {
        var query = EntityQueryEnumerator<BlobRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var blob, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            if (blob.Blobs.Count < 1)
                return;

            var result = Loc.GetString("blob-round-end-result", ("blobCount", blob.Blobs.Count));

            // yeah this is duplicated from traitor rules lol, there needs to be a generic rewrite where it just goes through all minds with objectives
            foreach (var (mindId, mind) in blob.Blobs)
            {
                var name = mind.CharacterName;
                _mindSystem.TryGetSession(mindId, out var session);
                var username = session?.Name;

                var objectives = mind.AllObjectives.ToArray();
                if (objectives.Length == 0)
                {
                    if (username != null)
                    {
                        if (name == null)
                            result += "\n" + Loc.GetString("blob-user-was-a-blob", ("user", username));
                        else
                        {
                            result += "\n" + Loc.GetString("blob-user-was-a-blob-named", ("user", username),
                                ("name", name));
                        }
                    }
                    else if (name != null)
                        result += "\n" + Loc.GetString("blob-was-a-blob-named", ("name", name));

                    continue;
                }

                if (username != null)
                {
                    if (name == null)
                    {
                        result += "\n" + Loc.GetString("blob-user-was-a-blob-with-objectives",
                            ("user", username));
                    }
                    else
                    {
                        result += "\n" + Loc.GetString("blob-user-was-a-blob-with-objectives-named",
                            ("user", username), ("name", name));
                    }
                }
                else if (name != null)
                    result += "\n" + Loc.GetString("blob-was-a-blob-with-objectives-named", ("name", name));

                foreach (var objectiveGroup in objectives.GroupBy(o => o.Prototype.Issuer))
                {
                    foreach (var objective in objectiveGroup)
                    {
                        foreach (var condition in objective.Conditions)
                        {
                            var progress = condition.Progress;
                            if (progress > 0.99f)
                            {
                                result += "\n- " + Loc.GetString(
                                    "traitor-objective-condition-success",
                                    ("condition", condition.Title),
                                    ("markupColor", "green")
                                );
                            }
                            else
                            {
                                result += "\n- " + Loc.GetString(
                                    "traitor-objective-condition-fail",
                                    ("condition", condition.Title),
                                    ("progress", (int) (progress * 100)),
                                    ("markupColor", "red")
                                );
                            }
                        }
                    }
                }
            }

            ev.AddLine(result);
        }
    }
}
