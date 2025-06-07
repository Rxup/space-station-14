using Content.Client.Popups;
using Content.Client.UserInterface.Controls;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.RCD;

[UsedImplicitly]
public sealed class RCDMenuBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;

    private SimpleRadialMenu? _menu;

    public RCDMenuBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent<RCDComponent>(Owner, out var rcd))
            return;

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);
        var models = ConvertToButtons(rcd.AvailablePrototypes);
        _menu.SetButtons(models);

        _menu.OpenOverMouseScreenPosition();
    }

    private IEnumerable<RadialMenuOption> ConvertToButtons(HashSet<ProtoId<RCDPrototype>> prototypes)
    {
        var categories = new Dictionary<string, (List<RadialMenuActionOption> Actions, SpriteSpecifier? Sprite, string? Tooltip)>();
        var options = new List<RadialMenuOption>();

        foreach (var protoId in prototypes)
        {
            var proto = _prototypeManager.Index(protoId);
            var button = new RadialMenuActionOption<RCDPrototype>(HandleMenuOptionClick, proto)
            {
                Sprite = proto.Sprite,
                ToolTip = GetTooltip(proto)
            };

            if (!_prototypeManager.TryIndex<RCDGroupPrototype>(proto.Category, out var group))
            {
                options.Add(button);
                continue;
            }

            if (!categories.TryGetValue(proto.Category, out var entry))
            {
                var sprite = group.Sprite;
                entry = (new List<RadialMenuActionOption>(), sprite, Loc.GetString(group.Name));
                categories[proto.Category] = entry;
            }

            entry.Actions.Add(button);
        }

        foreach (var (category, (actions, sprite, tooltip)) in categories)
        {
            options.Add(new RadialMenuNestedLayerOption(actions)
            {
                Sprite = sprite,
                ToolTip = tooltip
            });
        }

        return options;
    }

    private void HandleMenuOptionClick(RCDPrototype proto)
    {
        SendMessage(new RCDSystemMessage(proto.ID));

        if (_playerManager.LocalSession?.AttachedEntity == null)
            return;

        var msg = Loc.GetString("rcd-component-change-mode", ("mode", Loc.GetString(proto.SetName)));

        if (proto.Mode is RcdMode.ConstructTile or RcdMode.ConstructObject)
        {
            var name = Loc.GetString(proto.SetName);

            if (proto.Prototype != null && _prototypeManager.TryIndex(proto.Prototype, out var entProto, logError: false))
                name = entProto.Name;

            msg = Loc.GetString("rcd-component-change-build-mode", ("name", name));
        }

        EntMan.System<PopupSystem>().PopupClient(msg, Owner, _playerManager.LocalSession.AttachedEntity);
    }

    private string GetTooltip(RCDPrototype proto)
    {
        string tooltip;

        if (proto.Mode is RcdMode.ConstructTile or RcdMode.ConstructObject
            && proto.Prototype != null
            && _prototypeManager.TryIndex(proto.Prototype, out var entProto, logError: false))
        {
            tooltip = Loc.GetString(entProto.Name);
        }
        else
        {
            tooltip = Loc.GetString(proto.SetName);
        }

        tooltip = OopsConcat(char.ToUpper(tooltip[0]).ToString(), tooltip[1..]);

        return tooltip;
    }

    private static string OopsConcat(string a, string b)
    {
        // This exists to prevent Roslyn being clever and compiling something that fails sandbox checks.
        return a + b;
    }
}
