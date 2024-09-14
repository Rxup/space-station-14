using Content.Client.UserInterface.Systems.Ghost.Widgets;
using Content.Shared.Backmen.Ghost;
using Content.Shared.Ghost;
using Robust.Client.Player;
using Robust.Client.UserInterface;

namespace Content.Client.Backmen.Ghost;

public sealed class GhostReJoinSystem : SharedGhostReJoinSystem
{
    [Dependency] private readonly IUserInterfaceManager _userInterfaceManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();


    }

    private float Acc = 0;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        Acc += frameTime;
        if (Acc > 1)
        {
            Acc -= 1;

            var plr = _playerManager.LocalSession?.AttachedEntity;
            if (plr == null)
                return;

            if(!TryComp<GhostComponent>(plr, out var ghostComponent))
                return;

            var ui = _userInterfaceManager.GetActiveUIWidgetOrNull<GhostGui>();
            if(ui == null)
                return;

            var timeOffset = _gameTiming.CurTime - ghostComponent.TimeOfDeath;
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
            ui.ReturnToRound.Text = Loc.GetString("ghost-gui-return-to-round-button") + " " + (_ghostRespawnTime - timeOffset).ToString("mm\\:ss");
        }
    }
}
