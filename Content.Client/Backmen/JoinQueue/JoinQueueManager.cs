using Content.Shared.Backmen.JoinQueue;
using Robust.Client.State;
using Robust.Shared.Network;

namespace Content.Client.Backmen.JoinQueue;

public sealed class JoinQueueManager : Content.Corvax.Interfaces.Client.IClientJoinQueueManager
{
    [Dependency] private readonly IClientNetManager _netManager = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;

    public void Initialize()
    {
        _netManager.RegisterNetMessage<MsgQueueUpdate>(OnQueueUpdate);
    }

    private void OnQueueUpdate(MsgQueueUpdate msg)
    {
        if (_stateManager.CurrentState is not QueueState state)
        {
            state = _stateManager.RequestStateChange<QueueState>();
        }

        state.OnQueueUpdate(msg);
    }
}
