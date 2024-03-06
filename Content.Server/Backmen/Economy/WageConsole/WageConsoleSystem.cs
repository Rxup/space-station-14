using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Backmen.Economy.Wage;
using Content.Server.Popups;
using Content.Shared.Access.Systems;
using Content.Shared.Backmen.Economy.WageConsole;
using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;

namespace Content.Server.Backmen.Economy.WageConsole;

public sealed class WageConsoleSystem : SharedWageConsoleSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly WageManagerSystem _wageManager = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WageConsoleComponent,ActivatableUIOpenAttemptEvent>(OnTryOpenUi);
        Subs.BuiEvents<WageConsoleComponent>(WageUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<OpenWageRowMsg>(OnOpenWageRow);
            subs.Event<SaveEditedWageRowMsg>(OnEditWageRow);
            subs.Event<OpenBonusWageMsg>(OnOpenBonusRow);
            subs.Event<BonusWageRowMsg>(OnBonusMsg);
        });
    }

    private void OnTryOpenUi(Entity<WageConsoleComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!_access.IsAllowed(args.User, ent))
        {
            _popup.PopupCursor(Loc.GetString("reinforcement-insufficient-access"), args.User, PopupType.Medium);
            args.Cancel();
        }
    }

    private void OnBonusMsg(Entity<WageConsoleComponent> ent, ref BonusWageRowMsg args)
    {
        if (args.Session.AttachedEntity is null) { return; }

        if (!_access.IsAllowed(args.Session.AttachedEntity.Value, ent))
        {
            _popup.PopupCursor(Loc.GetString("wageconsole-insufficient-access"), args.Session, PopupType.Medium);
            return;
        }

        var id = args.Id;
        var wagePayout = _wageManager.PayoutsList.FirstOrDefault(x => x.Id == id);

        if (wagePayout == null)
        {
            return;
        }

        _adminLogger.Add(LogType.Transactions, LogImpact.Extreme,
            $"Player {args.Session.Name} use BonusSystem on accountId {wagePayout.ToAccountNumber.Comp.AccountNumber} with name {wagePayout.ToAccountNumber:entity} and add {args.Wage}");

        QueueLocalEvent(new WagePaydayEvent()
        {
            Mod = 1,
            Value = args.Wage,
            WhiteListTo = { wagePayout.ToAccountNumber }
        });
    }

    private void OnOpenBonusRow(Entity<WageConsoleComponent> ent, ref OpenBonusWageMsg args)
    {
        var id = args.Id;
        var wagePayout = _wageManager.PayoutsList.FirstOrDefault(x => x.Id == id);

        if (wagePayout == null)
        {
            return;
        }

        _ui.TrySetUiState(ent, WageUiKey.Key, new OpenBonusWageConsoleUi
        {
            Row = new UpdateWageRow
            {
                Id = wagePayout.Id,
                FromId = GetNetEntity(wagePayout.FromAccountNumber),
                FromName = Name(wagePayout.FromAccountNumber),
                FromAccount = wagePayout.FromAccountNumber.Comp.AccountNumber,
                ToId = GetNetEntity(wagePayout.ToAccountNumber),
                ToName = Name(wagePayout.ToAccountNumber),
                ToAccount = wagePayout.ToAccountNumber.Comp.AccountNumber,
                Wage = wagePayout.PayoutAmount,
            }
        });
    }

    private void OnEditWageRow(Entity<WageConsoleComponent> ent, ref SaveEditedWageRowMsg args)
    {
        if (args.Session.AttachedEntity is null) { return; }

        if (!_access.IsAllowed(args.Session.AttachedEntity.Value, ent))
        {
            _popup.PopupCursor(Loc.GetString("wageconsole-insufficient-access"), args.Session, PopupType.Medium);
            return;
        }

        var id = args.Id;
        var wagePayout = _wageManager.PayoutsList.FirstOrDefault(x => x.Id == id);

        if (wagePayout == null)
        {
            return;
        }

        _adminLogger.Add(LogType.Transactions, LogImpact.Extreme,
            $"Player {args.Session.Name} use EditPayoutSystem on accountId {wagePayout.ToAccountNumber.Comp.AccountNumber} with name {wagePayout.ToAccountNumber:entity} and set payout to {args.Wage}");

        wagePayout.PayoutAmount = args.Wage;
        UpdateUserInterface(ent);
    }

    private void OnOpenWageRow(Entity<WageConsoleComponent> ent, ref OpenWageRowMsg args)
    {
        var id = args.Id;
        var wagePayout = _wageManager.PayoutsList.FirstOrDefault(x => x.Id == id);

        if (wagePayout == null)
        {
            return;
        }

        _ui.TrySetUiState(ent, WageUiKey.Key, new OpenEditWageConsoleUi
        {
            Row = new UpdateWageRow
            {
                Id = wagePayout.Id,
                FromId = GetNetEntity(wagePayout.FromAccountNumber),
                FromName = Name(wagePayout.FromAccountNumber),
                FromAccount = wagePayout.FromAccountNumber.Comp.AccountNumber,
                ToId = GetNetEntity(wagePayout.ToAccountNumber),
                ToName = Name(wagePayout.ToAccountNumber),
                ToAccount = wagePayout.ToAccountNumber.Comp.AccountNumber,
                Wage = wagePayout.PayoutAmount,
            }
        });
    }

    private void UpdateUserInterface(Entity<WageConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUserInterface(ent);
    }

    private void UpdateUserInterface(Entity<WageConsoleComponent> ent)
    {
        var msg = new UpdateWageConsoleUi();

        foreach (var wagePayout in _wageManager.PayoutsList)
        {
            msg.Records.Add(new UpdateWageRow
            {
                Id = wagePayout.Id,

                FromId = GetNetEntity(wagePayout.FromAccountNumber),
                FromName = Name(wagePayout.FromAccountNumber),
                FromAccount = wagePayout.FromAccountNumber.Comp.AccountNumber,
                ToId = GetNetEntity(wagePayout.ToAccountNumber),
                ToName = Name(wagePayout.ToAccountNumber),
                ToAccount = wagePayout.ToAccountNumber.Comp.AccountNumber,
                Wage = wagePayout.PayoutAmount,
            });
        }

        _ui.TrySetUiState(ent, WageUiKey.Key, msg);
    }
}
