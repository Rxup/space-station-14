using Content.Shared.Silicons.Borgs.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Content.Shared.Backmen.Silicons.Borgs;

namespace Content.Client.Silicons.Borgs;

/// <summary>
/// User interface used by borgs to select their type.
/// </summary>
/// <seealso cref="BorgSelectTypeMenu"/>
/// <seealso cref="BorgSwitchableTypeComponent"/>
/// <seealso cref="BorgSwitchableTypeUiKey"/>
[UsedImplicitly]
public sealed class BorgSelectTypeUserInterface : BoundUserInterface
{
    [ViewVariables]
    private BorgSelectTypeMenu? _menu;

    public BorgSelectTypeUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<BorgSelectTypeMenu>();
        _menu.ConfirmedBorgType += prototype => SendPredictedMessage(new BorgSelectTypeMessage(prototype));
        _menu.ConfirmedBorgSubtype += subtype => SendPredictedMessage(new BorgSelectSubtypeMessage(subtype)); // Backmen
    }
}
