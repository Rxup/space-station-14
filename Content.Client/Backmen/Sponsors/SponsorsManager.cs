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

        OocColor = Color.TryFromHex(message.Info.OOCColor);
        Prototypes.AddRange(message.Info.AllowedMarkings);
        PriorityJoin = message.Info.HavePriorityJoin;
        ExtraCharSlots = message.Info.ExtraSlots;
        GhostTheme = message.Info.GhostTheme;
    }

    private void Reset()
    {
        Prototypes.Clear();
        PriorityJoin = false;
        OocColor = null;
        ExtraCharSlots = 0;
        GhostTheme = null;
    }


    public List<string> Prototypes { get; } = new();
    public bool PriorityJoin { get; private set; }
    public Color? OocColor { get; private set; }
    public int ExtraCharSlots { get; private set; }
    public string? GhostTheme { get; private set; }
}
