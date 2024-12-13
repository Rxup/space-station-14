using System.Collections.Immutable;
using Content.Corvax.Interfaces.Server;
using Content.Server.GameTicking.Events;
using Content.Server.Station.Events;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Players;

public sealed class DiscordJobWhitelist : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IServerDiscordAuthManager _manager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private ImmutableArray<ProtoId<JobPrototype>> _whitelistedJobs = [];

    public override void Initialize()
    {
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<StationJobsGetCandidatesEvent>(OnStationJobsGetCandidates);
        SubscribeLocalEvent<IsJobAllowedEvent>(OnIsJobAllowed);
        SubscribeLocalEvent<GetDisallowedJobsEvent>(OnGetDisallowedJobs);

        CacheJobs();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs ev)
    {
        if (ev.WasModified<JobPrototype>())
            CacheJobs();
    }

    private void OnStationJobsGetCandidates(ref StationJobsGetCandidatesEvent ev)
    {
        if (!_config.GetCVar(CCVars.DiscordAuthEnabled))
            return;

        for (var i = ev.Jobs.Count - 1; i >= 0; i--)
        {
            var job = ev.Jobs[i];
            if (_whitelistedJobs.Contains(job) &&
                _player.TryGetSessionById(ev.Player, out var player) &&
                !_manager.IsCached(player))
            {
                ev.Jobs.RemoveSwap(i);
            }
        }
    }

    private void OnIsJobAllowed(ref IsJobAllowedEvent ev)
    {
        if (!_config.GetCVar(CCVars.DiscordAuthEnabled))
            return;

        if (_whitelistedJobs.Contains(ev.JobId) && !_manager.IsCached(ev.Player))
            ev.Cancelled = true;
    }

    private void OnGetDisallowedJobs(ref GetDisallowedJobsEvent ev)
    {
        if (!_config.GetCVar(CCVars.DiscordAuthEnabled))
            return;

        foreach (var job in _whitelistedJobs)
        {
            if (!_manager.IsCached(ev.Player))
                ev.Jobs.Add(job);
        }
    }

    private void CacheJobs()
    {
        var builder = ImmutableArray.CreateBuilder<ProtoId<JobPrototype>>();
        foreach (var job in _prototypes.EnumeratePrototypes<JobPrototype>())
        {
            if (job.DiscordRequired)
                builder.Add(job.ID);
        }

        _whitelistedJobs = builder.ToImmutable();
    }
}
