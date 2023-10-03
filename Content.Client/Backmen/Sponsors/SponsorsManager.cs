using System.Diagnostics.CodeAnalysis;
using Content.Corvax.Interfaces.Client;
using Content.Shared.Backmen.Sponsors;
using Robust.Shared.Network;

namespace Content.Client.Backmen.Sponsors;

public sealed class SponsorsManager : IClientSponsorsManager
{
    [Dependency] private readonly IClientNetManager _netMgr = default!;

    public void Initialize()
    {
        _netMgr.RegisterNetMessage<MsgSponsorInfo>(OnUpdate);
    }

    private void OnUpdate(MsgSponsorInfo message)
    {
        Reset();

        if (message.Info == null)
        {
            return;
        }

        Prototypes.AddRange(message.Info.AllowedMarkings);
    }

    private void Reset()
    {
        Prototypes.Clear();
    }

    public List<string> Prototypes { get; } = new();
}
