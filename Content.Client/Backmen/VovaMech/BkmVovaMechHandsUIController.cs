using Content.Client.Gameplay;
using Content.Client.Hands.Systems;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Hands.Controls;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Content.Client.Verbs.UI;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Input;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Timing;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.Backmen.VovaMech;

/// <summary>
/// Separate hand bar for OneStar mech innate tools, shown above the player hotbar while piloting.
/// </summary>
public sealed partial class BkmVovaMechHandsUIController : UIController, IOnStateEntered<GameplayState>, IOnSystemChanged<BkmVovaMechSystem>
{
    [Dependency] private IEntityManager _entities = default!;

    [UISystemDependency] private readonly HandsSystem _handsSystem = default!;
    [UISystemDependency] private readonly UseDelaySystem _useDelay = default!;

    private EntityUid? _mechUid;
    private HandsComponent? _mechHands;
    private HandButton? _activeHand;

    private HotbarGui? Hotbar => UIManager.GetActiveUIWidgetOrNull<HotbarGui>();

    public void OnSystemLoaded(BkmVovaMechSystem system)
    {
        system.LocalPilotedMechChanged += OnLocalPilotedMechChanged;

        _handsSystem.OnPlayerAddHand += OnMechHandAdded;
        _handsSystem.OnPlayerRemoveHand += OnMechHandRemoved;
    }

    public void OnSystemUnloaded(BkmVovaMechSystem system)
    {
        system.LocalPilotedMechChanged -= OnLocalPilotedMechChanged;

        _handsSystem.OnPlayerAddHand -= OnMechHandAdded;
        _handsSystem.OnPlayerRemoveHand -= OnMechHandRemoved;
    }

    public void OnStateEntered(GameplayState state)
    {
        if (_mechUid is { } mech && _mechHands is { } hands)
            LoadMechHands(mech, hands);
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_mechUid is not { } mech || _mechHands == null || Hotbar?.MechHandContainer is not { } container)
            return;

        SetActiveHand(_mechHands.ActiveHandId);

        foreach (var hand in container.GetButtons())
            RefreshHandButton(mech, hand.SlotName, hand);

        foreach (var hand in container.GetButtons())
        {
            if (!_entities.TryGetComponent(hand.Entity, out UseDelayComponent? useDelay))
            {
                hand.CooldownDisplay.Visible = false;
                continue;
            }

            var delay = _useDelay.GetLastEndingDelay((hand.Entity.Value, useDelay));
            hand.CooldownDisplay.Visible = true;
            hand.CooldownDisplay.FromTime(delay.StartTime, delay.EndTime);
        }
    }

    private void OnLocalPilotedMechChanged(EntityUid? mech)
    {
        if (mech == null)
        {
            UnloadMechHands();
            return;
        }

        if (!_entities.TryGetComponent(mech, out HandsComponent? hands))
        {
            _mechUid = mech;
            _mechHands = null;
            return;
        }

        LoadMechHands(mech.Value, hands);
    }

    private void OnMechHandAdded(Entity<HandsComponent> entity, string name, HandLocation location)
    {
        if (entity.Owner != _mechUid || _mechHands == null)
            return;

        if (!_handsSystem.TryGetHand((entity.Owner, entity.Comp), name, out var hand))
            return;

        AddHandButton(name, hand.Value);
        SetActiveHand(_mechHands.ActiveHandId);
    }

    private void OnMechHandRemoved(Entity<HandsComponent> entity, string name)
    {
        if (entity.Owner != _mechUid || Hotbar?.MechHandContainer is not { } container)
            return;

        container.TryRemoveButton(name, out _);
        SetActiveHand(_mechHands?.ActiveHandId);
    }

    private void LoadMechHands(EntityUid mech, HandsComponent hands)
    {
        UnloadMechHands();

        _mechUid = mech;
        _mechHands = hands;

        if (Hotbar == null)
            return;

        Hotbar.MechHandsRow.Visible = true;
        Hotbar.MechHandContainer.PlayerHandsComponent = hands;
        Hotbar.MechHandContainer.ClearButtons();

        foreach (var handId in hands.SortedHands)
        {
            if (!_handsSystem.TryGetHand((mech, hands), handId, out var hand))
                continue;

            AddHandButton(handId, hand.Value);
        }

        SetActiveHand(hands.ActiveHandId);
    }

    private void UnloadMechHands()
    {
        _mechUid = null;
        _mechHands = null;
        _activeHand = null;

        if (Hotbar == null)
            return;

        Hotbar.MechHandsRow.Visible = false;
        Hotbar.MechHandContainer.ClearButtons();
        Hotbar.MechHandContainer.PlayerHandsComponent = null;
    }

    private void AddHandButton(string handId, Hand hand)
    {
        if (Hotbar?.MechHandContainer is not { } || _mechUid is not { } mech)
            return;

        var button = new HandButton(handId, hand.Location);
        button.Pressed += HandPressed;

        Hotbar.MechHandContainer.TryAddButton(button);
        RefreshHandButton(mech, handId, button);
    }

    private void RefreshHandButton(EntityUid mech, string handId, HandButton button)
    {
        if (_mechHands == null)
            return;

        if (_handsSystem.TryGetHeldItem((mech, _mechHands), handId, out var held) &&
            _entities.TryGetComponent(held, out VirtualItemComponent? virt))
        {
            button.SetEntity(virt.BlockingEntity);
            button.Blocked = true;
            return;
        }

        button.SetEntity(held);
        button.Blocked = false;
    }

    private void HandPressed(GUIBoundKeyEventArgs args, SlotControl hand)
    {
        if (_mechUid is not { } mech || _mechHands == null)
            return;

        var handsEnt = (mech, _mechHands);

        if (args.Function == EngineKeyFunctions.UIClick)
        {
            MechHandClick(handsEnt, hand.SlotName);
            SetActiveHand(_mechHands.ActiveHandId);
            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.UseSecondary)
        {
            if (_handsSystem.TryGetHeldItem(handsEnt, hand.SlotName, out var held))
                UIManager.GetUIController<VerbMenuUIController>().OpenVerbMenu(held.Value);

            args.Handle();
        }
        else if (args.Function == ContentKeyFunctions.ActivateItemInWorld)
        {
            _handsSystem.TryActivateItemInHand(mech, _mechHands, hand.SlotName);
            args.Handle();
        }
        else if (args.Function == ContentKeyFunctions.AltActivateItemInWorld)
        {
            _handsSystem.TryUseItemInHand(mech, altInteract: true, _mechHands, hand.SlotName);
            args.Handle();
        }
    }

    private void MechHandClick(Entity<HandsComponent> ent, string handName)
    {
        var hands = ent.Comp;
        if (hands.ActiveHandId == null)
            return;

        var pressedEntity = _handsSystem.GetHeldItem(ent.AsNullable(), handName);
        var activeEntity = _handsSystem.GetActiveItem(ent.AsNullable());

        if (handName == hands.ActiveHandId && activeEntity != null)
        {
            _handsSystem.TryUseItemInHand(ent.Owner);
            return;
        }

        if (handName != hands.ActiveHandId && pressedEntity == null)
        {
            _handsSystem.SetActiveHand(ent.AsNullable(), handName);
            return;
        }

        if (handName != hands.ActiveHandId && pressedEntity != null && activeEntity != null)
        {
            _handsSystem.TryInteractHandWithActiveHand(ent.Owner, handName, hands);
            return;
        }

        if (handName != hands.ActiveHandId && pressedEntity != null && activeEntity == null)
        {
            _handsSystem.TryMoveHeldEntityToActiveHand(ent.Owner, handName, handsComp: hands);
        }
    }

    private void SetActiveHand(string? handName)
    {
        if (handName == null)
        {
            _activeHand?.Highlight = false;
            _activeHand = null;
            return;
        }

        if (Hotbar?.MechHandContainer.TryGetButton(handName, out var handControl) != true || handControl == _activeHand)
            return;

        _activeHand?.Highlight = false;
        handControl!.Highlight = true;
        _activeHand = handControl;
    }
}
