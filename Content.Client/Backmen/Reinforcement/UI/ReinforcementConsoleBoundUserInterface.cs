using Content.Shared.Access.Systems;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Backmen.Reinforcement;
using Content.Shared.Backmen.Reinforcement.Components;

namespace Content.Client.Backmen.Reinforcement.UI;

[UsedImplicitly]
public sealed class ReinforcementConsoleBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    private readonly AccessReaderSystem _accessReader;

    private ReinforcementConsoleWindow? _window;

    public ReinforcementConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _accessReader = EntMan.System<AccessReaderSystem>();
    }

    protected override void Open()
    {
        base.Open();

        var comp = EntMan.GetComponent<ReinforcementConsoleComponent>(Owner);

        _window = new(Owner, comp, _playerManager, _proto, _random, _accessReader);
        _window.OnKeySelected += (key,count) => SendMessage(new ChangeReinforcementMsg(key,count));
        _window.OnBriefChange += (brief) => SendMessage(new BriefReinforcementUpdate(brief));
        _window.OnStartCall += () => SendMessage(new CallReinforcementStart());
        _window.OnClose += Close;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is UpdateReinforcementUi s)
        {
            _window?.Update(s);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _window?.Close();
    }
}
