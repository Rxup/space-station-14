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

    /// <summary>
    /// Cycles to the next lobby background and syncs it to all clients.
    /// </summary>
    public bool TryNextLobbyBackground(out ProtoId<AnimatedLobbyScreenPrototype>? background)
    {
        background = LobbyBackground;

        if (_lobbyBackgrounds == null || _lobbyBackgrounds.Count == 0)
            return false;

        var index = LobbyBackground == null
            ? -1
            : _lobbyBackgrounds.FindIndex(id => id == LobbyBackground.Value);

        LobbyBackground = _lobbyBackgrounds[(index + 1) % _lobbyBackgrounds.Count];
        background = LobbyBackground;
        SendStatusToAll();
        return true;
    }

    /// <summary>
    /// Sets a specific lobby background and syncs it to all clients.
    /// </summary>
    public bool TrySetLobbyBackground(ProtoId<AnimatedLobbyScreenPrototype> background)
    {
        if (_lobbyBackgrounds == null || !_lobbyBackgrounds.Contains(background))
            return false;

        LobbyBackground = background;
        SendStatusToAll();
        return true;
    }
}
