﻿using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Corvax.Interfaces.Client;
using Content.Corvax.Interfaces.Shared;
using Content.Shared.Backmen.Sponsors;
using Robust.Shared.Network;

namespace Content.Client.Backmen.Sponsors;

public sealed class SponsorsManager : ISharedSponsorsManager
{
    [Dependency] private readonly IClientNetManager _netMgr = default!;

    public void Initialize()
    {
        _netMgr.RegisterNetMessage<MsgSponsorInfo>(OnUpdate);
        _netMgr.RegisterNetMessage<Shared.Backmen.MsgWhitelist>(RxWhitelist); //backmen: whitelist
    }

    public List<string> GetClientPrototypes()
    {
        return Prototypes.ToList();
    }

    public void Cleanup()
    {
        Reset();
    }

    private void RxWhitelist(Shared.Backmen.MsgWhitelist message)
    {
        Whitelisted = message.Whitelisted;
    }

    private void OnUpdate(MsgSponsorInfo message)
    {
        Reset();

#if DEBUG
        Prototypes.Add("tier1");
        Prototypes.Add("tier2");
        Prototypes.Add("tier01");
        Prototypes.Add("tier02");
        Prototypes.Add("tier03");
        Prototypes.Add("tier04");
        Prototypes.Add("tier05");
#endif

        if (message.Info == null)
        {
            return;
        }

        foreach (var markings in message.Info.AllowedMarkings)
        {
            Prototypes.Add(markings);
        }

        Tier = message.Info.Tier ?? 0;
        GhostTheme = message.Info.GhostTheme;
    }

    private void Reset()
    {
        Prototypes.Clear();
        Tier = 0;
        GhostTheme = null;
    }

    public HashSet<string> Prototypes { get; } = new();
    public int Tier { get; private set; } = 0;
    public bool Whitelisted { get; private set; } = false;
    public string? GhostTheme { get; private set; } = null;

}
