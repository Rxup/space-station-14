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

    private ReinforcementConsoleWindow? _window;

    public ReinforcementConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {

    }

    protected override void Open()
    {
        base.Open();

        var comp = EntMan.GetComponent<ReinforcementConsoleComponent>(Owner);

        var stationName = "";
        if (EntMan.TryGetComponent<TransformComponent>(Owner, out var xform) && xform.GridUid != null && EntMan.TryGetComponent<MetaDataComponent>(xform.GridUid, out var stationData))
        {
            stationName = stationData.EntityName;
        }

        _window = new(Owner, comp, _proto, stationName);
        _window.OnKeySelected += (key,count) => SendMessage(new ChangeReinforcementMsg(key,count));
        _window.OnBriefChange += (brief) => SendMessage(new BriefReinforcementUpdate(brief[..Math.Min(brief.Length,comp.MaxStringLength)]));
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
