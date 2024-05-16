using Content.Corvax.Interfaces.Server;
using Content.Corvax.Interfaces.Shared;
using Content.Shared.GameTicking;
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
    [Dependency] private readonly ISharedSponsorsManager _sponsorsManager = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerJoinMoveToGameEvent>(OnPlayerJoinMoveToGame);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
    }

    private void OnCleanup(RoundRestartCleanupEvent msg)
    {
        Log.Debug("do sponsor cleanup");
        _sponsorsManager.Cleanup();
    }

    private void OnPlayerJoinMoveToGame(PlayerJoinMoveToGameEvent ev)
    {
        Log.Info($"player via event move to game {ev.Player.Name}");
        _playerManager.JoinGame(ev.Player);
    }

    public void JoinGame(ICommonSession sess)
    {
        Log.Info($"player queue move to game {sess.Name}");
        QueueLocalEvent(new PlayerJoinMoveToGameEvent(sess));
    }
}
