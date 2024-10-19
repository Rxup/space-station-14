using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Server.Administration.Logs;
using Robust.Shared.Console;
using Content.Server.Backmen.Economy;
using System.Linq;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Store;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class SetBankCurrencyCommand : IConsoleCommand
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command { get; } = "setbankcurrency";
    public string Description { get; } = "Изменить банковский счет";
    public string Help { get; } = "setbankcurrency <account#> <value>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var bankManagerSystem = _entityManager.System<BankManagerSystem>();

        if (args.Length == 0)
        {
            shell.WriteError(Help);
            return;
        }

        if (!bankManagerSystem.TryGetBankAccount(args[0], out var bankAccount))
        {
            shell.WriteError("Банковский счет не найден!");
            return;
        }

        if (args.Length == 1)
        {
            shell.WriteLine($"Банковский баланс({bankAccount.Value.Comp.AccountName}): {bankAccount.Value.Comp.Balance} {bankAccount.Value.Comp.CurrencyType}");
            return;
        }

        if (!int.TryParse(args[1], out var balance))
        {
            shell.WriteError($"Значение {args[1]} не число!");
            return;
        }

        switch (balance)
        {
            case > 0:
            {
                if (!bankManagerSystem.TryInsertToBankAccount(bankAccount,
                        new KeyValuePair<ProtoId<CurrencyPrototype>, FixedPoint2>(bankAccount.Value.Comp.CurrencyType, FixedPoint2.New(balance))))
                {
                    shell.WriteError($"Добавить на счет не удалось! Баланс аккаунта: {bankAccount.Value.Comp.Balance}");
                    return;
                }

                break;
            }
            case < 0:
            {
                if (!bankManagerSystem.TryWithdrawFromBankAccount(bankAccount,
                        new KeyValuePair<ProtoId<CurrencyPrototype>, FixedPoint2>(bankAccount.Value.Comp.CurrencyType, FixedPoint2.New(Math.Abs(balance)))))
                {
                    shell.WriteError($"Списать со счета не удалось! Баланс аккаунта: {bankAccount.Value.Comp.Balance}");
                    return;
                }

                break;
            }
            default:
                bankManagerSystem.TrySetBalance(bankAccount.Value,balance);
                return;
        }

        _adminLogger.Add(LogType.AdminMessage, LogImpact.Extreme,
            $"Admin {(shell.Player != null ? shell.Player.Name : "An administrator")} SetBankCurrency {bankAccount.Value.Comp.AccountName} #{bankAccount.Value.Comp.AccountNumber} changed by: {balance}, new balance: {bankAccount.Value.Comp.Balance}");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(
                _entityManager.System<BankManagerSystem>().ActiveBankAccounts
                    .Select(x => new CompletionOption(x.Value.Comp.AccountNumber, x.Value.Comp.AccountName))
                , "Аккаунт №"),
            _ => CompletionResult.Empty
        };
    }
}
