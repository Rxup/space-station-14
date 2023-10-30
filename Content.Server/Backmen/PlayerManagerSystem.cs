using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server.Backmen;

public sealed class PlayerJoinMoveToGameEvent : EntityEventArgs
{
    public PlayerJoinMoveToGameEvent(ICommonSession player)
    {
        Player = player;
    }
    public ICommonSession Player { get; }
}

public sealed class PlayerManagerSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ILogManager _logManager = default!;


    private ISawmill _sawmill = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerJoinMoveToGameEvent>(OnPlayerJoinMoveToGame);
        _sawmill = _logManager.GetSawmill("backmen.plrman");
    }

    private void OnPlayerJoinMoveToGame(PlayerJoinMoveToGameEvent ev)
    {
        _sawmill.Info($"player via event move to game {ev.Player.Name}");
        _playerManager.JoinGame(ev.Player);
    }

    public void JoinGame(ICommonSession sess)
    {
        _sawmill.Info($"player queue move to game {sess.Name}");
        QueueLocalEvent(new PlayerJoinMoveToGameEvent(sess));
    }
}
