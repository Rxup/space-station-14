using Content.Shared.GameTicking.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;
using Content.Shared.Backmen.Lobby;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    [ViewVariables]
    public ProtoId<AnimatedLobbyScreenPrototype>? LobbyBackground { get; private set; }

    [ViewVariables]
    private List<ProtoId<AnimatedLobbyScreenPrototype>>? _lobbyBackgrounds;

    private void InitializeLobbyBackground()
    {
        _lobbyBackgrounds = _prototypeManager.EnumeratePrototypes<AnimatedLobbyScreenPrototype>()
            .Select(proto=>new ProtoId<AnimatedLobbyScreenPrototype>(proto.ID))
            .ToList();

        RandomizeLobbyBackground();
    }

    private void RandomizeLobbyBackground()
    {
        if (_lobbyBackgrounds != null && _lobbyBackgrounds.Count != 0)
            LobbyBackground = _robustRandom.Pick(_lobbyBackgrounds);
        else
            LobbyBackground = null;
    }
}
