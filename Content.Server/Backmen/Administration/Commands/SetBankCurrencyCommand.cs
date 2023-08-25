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

        if (args.Length != 2)
        {
            shell.WriteError(Help);
            return;
        }

        if (!bankManagerSystem.IsBankAccountExists(args[0]))
        {
            shell.WriteError("Банковский счет не найден!");
            return;
        }

        if (!int.TryParse(args[1], out var balance))
        {
            shell.WriteError($"Значение {args[1]} не число!");
            return;
        }

        var account = bankManagerSystem.GetBankAccount(args[0])!;

        switch (balance)
        {
            case > 0:
            {
                if (!bankManagerSystem.TryInsertToBankAccount(account.AccountNumber,
                        new KeyValuePair<string, FixedPoint2>(account.CurrencyType, FixedPoint2.New(balance))))
                {
                    shell.WriteError($"Добавить на счет не удалось! Баланс аккаунта: {account.Balance}");
                    return;
                }

                break;
            }
            case < 0:
            {
                if (!bankManagerSystem.TryWithdrawFromBankAccount(account.AccountNumber, account.AccountPin,
                        new KeyValuePair<string, FixedPoint2>(account.CurrencyType, FixedPoint2.New(Math.Abs(balance)))))
                {
                    shell.WriteError($"Списать со счета не удалось! Баланс аккаунта: {account.Balance}");
                    return;
                }

                break;
            }
            default:
                bankManagerSystem.TrySetBalance(account.Owner,account.Balance);
                return;
        }



        _adminLogger.Add(LogType.AdminMessage, LogImpact.Extreme,
            $"Admin {(shell.Player != null ? shell.Player.Name : "An administrator")} SetBankCurrency {account.AccountName} #{account.AccountNumber} changed by: {balance}, new balance: {account.Balance}");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(
                _entityManager.System<BankManagerSystem>().ActiveBankAccounts
                    .Select(x => new CompletionOption(x.Value.AccountNumber, x.Value.AccountName))
                , "Аккаунт №"),
            _ => CompletionResult.Empty
        };
    }
}
