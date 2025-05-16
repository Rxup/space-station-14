using Content.Client.Backmen.UserInterface.Systems.Targeting.Widgets;
using Content.Client.Gameplay;
using Content.Client.Backmen.Targeting;
using Content.Shared.Backmen.Targeting;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.Player;
using Robust.Shared.Utility;

namespace Content.Client.Backmen.UserInterface.Systems.Targeting;

public sealed class TargetingUIController : UIController, IOnStateEntered<GameplayState>, IOnSystemChanged<TargetingSystem>
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IEntityNetworkManager _net = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private SpriteSystem? _spriteSystem;
    private TargetingComponent? _targetingComponent;
    private TargetingControl? TargetingControl => UIManager.GetActiveUIWidgetOrNull<TargetingControl>();

    public void OnSystemLoaded(TargetingSystem system)
    {
        system.TargetingStartup += AddTargetingControl;
        system.TargetingShutdown += RemoveTargetingControl;
        system.TargetChange += CycleTarget;
        system.PartStatusUpdate += UpdatePartStatusControl;
    }

    public void OnSystemUnloaded(TargetingSystem system)
    {
        system.TargetingStartup -= AddTargetingControl;
        system.TargetingShutdown -= RemoveTargetingControl;
        system.TargetChange -= CycleTarget;
        system.PartStatusUpdate -= UpdatePartStatusControl;
    }

    public void OnStateEntered(GameplayState state)
    {
        if (TargetingControl == null)
            return;

        TargetingControl.SetTargetDollVisible(_targetingComponent != null);

        if (_targetingComponent == null)
            return;

        TargetingControl.SetBodyPartsVisible(_targetingComponent.Target);
        TargetingControl.SetTextures(_targetingComponent.BodyStatus);
    }

    public void AddTargetingControl(TargetingComponent component)
    {
        _targetingComponent = component;

        if (TargetingControl == null)
            return;

        TargetingControl.SetTargetDollVisible(_targetingComponent != null);

        if (_targetingComponent == null)
            return;

        TargetingControl.SetBodyPartsVisible(_targetingComponent.Target);
        TargetingControl.SetTextures(_targetingComponent.BodyStatus);
    }

    public void RemoveTargetingControl()
    {
        TargetingControl?.SetTargetDollVisible(false);
        _targetingComponent = null;
    }

    public void CycleTarget(TargetBodyPart bodyPart)
    {
        if (_playerManager.LocalEntity is not { } user
            || _entManager.GetComponent<TargetingComponent>(user) is not { } targetingComponent
            || TargetingControl == null
            || bodyPart == targetingComponent.Target)
            return;

        var player = _entManager.GetNetEntity(user);
        var msg = new TargetChangeEvent(player, bodyPart);
        _net.SendSystemNetworkMessage(msg);
        TargetingControl?.SetBodyPartsVisible(bodyPart);
    }

    public void UpdatePartStatusControl(TargetingComponent component)
    {
        if (TargetingControl != null && _targetingComponent != null)
            TargetingControl.SetTextures(_targetingComponent.BodyStatus);
    }

    public Texture GetTexture(SpriteSpecifier specifier)
    {
        _spriteSystem ??= _entManager.System<SpriteSystem>();
        return _spriteSystem.Frame0(specifier);
    }
}
