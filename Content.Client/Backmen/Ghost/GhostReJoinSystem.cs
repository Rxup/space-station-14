using Content.Client.UserInterface.Systems.Ghost.Widgets;
using Content.Shared.Backmen.Ghost;
using Content.Shared.Ghost;
using Robust.Client.Player;
using Robust.Client.UserInterface;

namespace Content.Client.Backmen.Ghost;

public sealed partial class GhostReJoinSystem : SharedGhostReJoinSystem
{
    [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();


    }

    private float _acc = 0;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _acc += frameTime;
        if (_acc <= 1)
            return;
        _acc -= 1;

        var plr = _playerManager.LocalSession?.AttachedEntity;
        if (plr == null)
            return;

        if(!TryComp<GhostComponent>(plr, out var ghostComponent))
            return;

        var ui = _userInterfaceManager.GetActiveUIWidgetOrNull<GhostGui>();
        if(ui == null)
            return;

        var timeOffset = GetTimeSinceDeath(_gameTiming, ghostComponent.TimeOfDeath);
        if (timeOffset >= _ghostRespawnTime)
        {
            if (ui.ReturnToRound.Disabled)
            {
                ui.ReturnToRound.Disabled = false;
                ui.ReturnToRound.Text = Loc.GetString("ghost-gui-return-to-round-button");
            }

            return;
        }

        ui.ReturnToRound.Disabled = true;
        ui.ReturnToRound.Text = Loc.GetString("ghost-gui-return-to-round-button") + " " +
                                 FormatRespawnTimeRemaining(GetRespawnTimeRemaining(_ghostRespawnTime, timeOffset));
    }
}
