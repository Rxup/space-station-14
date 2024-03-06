using System.Linq;
using Content.Server.Backmen.Economy.Wage;
using Content.Shared.Backmen.Economy.WageConsole;
using Robust.Server.GameObjects;

namespace Content.Server.Backmen.Economy.WageConsole;

public sealed class WageConsoleSystem : SharedWageConsoleSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly WageManagerSystem _wageManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<WageConsoleComponent>(WageUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<OpenWageRowMsg>(OnOpenWageRow);
            subs.Event<SaveEditedWageRowMsg>(OnEditWageRow);
            subs.Event<OpenBonusWageMsg>(OnOpenBonusRow);
            subs.Event<BonusWageRowMsg>(OnBonusMsg);
        });
    }

    private void OnBonusMsg(Entity<WageConsoleComponent> ent, ref BonusWageRowMsg args)
    {
        var id = args.Id;
        var wagePayout = _wageManager.PayoutsList.FirstOrDefault(x => x.Id == id);

        if (wagePayout == null)
        {
            return;
        }

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
        var id = args.Id;
        var wagePayout = _wageManager.PayoutsList.FirstOrDefault(x => x.Id == id);

        if (wagePayout == null)
        {
            return;
        }

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
