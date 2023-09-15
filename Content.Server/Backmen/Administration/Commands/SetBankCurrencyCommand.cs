using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Server.Administration.Logs;
using Robust.Shared.Console;
using Content.Server.Backmen.Economy;
using System.Linq;
using Content.Shared.Database;
using Content.Shared.FixedPoint;

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

        if (!bankManagerSystem.TryGetBankAccount(args[0], out var bankAccountOwner, out var bankAccount))
        {
            shell.WriteError("Банковский счет не найден!");
            return;
        }

        if (args.Length == 1)
        {
            shell.WriteLine($"Банковский баланс({bankAccount.AccountName}): {bankAccount.Balance} {bankAccount.CurrencyType}");
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
                if (!bankManagerSystem.TryInsertToBankAccount(bankAccountOwner,
                        new KeyValuePair<string, FixedPoint2>(bankAccount.CurrencyType, FixedPoint2.New(balance)), bankAccount))
                {
                    shell.WriteError($"Добавить на счет не удалось! Баланс аккаунта: {bankAccount.Balance}");
                    return;
                }

                break;
            }
            case < 0:
            {
                if (!bankManagerSystem.TryWithdrawFromBankAccount(bankAccountOwner,
                        new KeyValuePair<string, FixedPoint2>(bankAccount.CurrencyType, FixedPoint2.New(Math.Abs(balance))), bankAccount))
                {
                    shell.WriteError($"Списать со счета не удалось! Баланс аккаунта: {bankAccount.Balance}");
                    return;
                }

                break;
            }
            default:
                bankManagerSystem.TrySetBalance(bankAccountOwner,balance);
                return;
        }

        _adminLogger.Add(LogType.AdminMessage, LogImpact.Extreme,
            $"Admin {(shell.Player != null ? shell.Player.Name : "An administrator")} SetBankCurrency {bankAccount.AccountName} #{bankAccount.AccountNumber} changed by: {balance}, new balance: {bankAccount.Balance}");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(
                _entityManager.System<BankManagerSystem>().ActiveBankAccounts
                    .Select(x => new CompletionOption(x.Value.account.AccountNumber, x.Value.account.AccountName))
                , "Аккаунт №"),
            _ => CompletionResult.Empty
        };
    }
}
