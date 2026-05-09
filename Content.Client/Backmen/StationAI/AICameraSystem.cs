using Content.Shared.Backmen.StationAI;
using Content.Shared.Backmen.StationAI.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Client.GameObjects;
using Robust.Client.Player;

namespace Content.Client.Backmen.StationAI;

public sealed partial class AICameraSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _userInterfaceSystem = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AIEyeComponent, AfterAutoHandleStateEvent>(OnCamUpdated);
    }

    private void OnCamUpdated(Entity<AIEyeComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if(_playerManager.LocalEntity == null)
            return;

        if (_userInterfaceSystem.TryGetOpenUi(_playerManager.LocalEntity.Value, AICameraListUiKey.Key, out var bui))
        {
            bui.Update();
        }
    }
}
