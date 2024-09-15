using Content.Server.EUI;
using Content.Shared.Backmen.Ghost;
using Content.Shared.Eui;
using Content.Shared.Ghost;
using Robust.Shared.Player;

namespace Content.Server.Backmen.Ghost;

public sealed class GhostReJoinEui : BaseEui
{
    private readonly GhostReJoinSystem _ghostReJoinSystem;
    private readonly Entity<GhostComponent> _entity;

    public GhostReJoinEui(GhostReJoinSystem ghostReJoinSystem, Entity<GhostComponent> entity)
    {
        _ghostReJoinSystem = ghostReJoinSystem;
        _entity = entity;
    }

    public override EuiStateBase GetNewState()
    {
        return _ghostReJoinSystem.UpdateUserInterface(_entity);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        if (msg is GhostReJoinCharacterMessage msg1)
        {
            _ghostReJoinSystem.OnJoinSelected(this,_entity, ref msg1);
        }
        else if (msg is GhostReJoinRandomMessage msg2)
        {
            _ghostReJoinSystem.OnJoinRandom(this,_entity, ref msg2);
        }
    }
}
