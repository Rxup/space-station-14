using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Backmen.Economy;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Robust.Server.GameObjects;
using Robust.Shared.Console;

namespace Content.Server.Backmen.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class AddBankAcсountCommand : IConsoleCommand
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;


    public string Command { get; } = "addbankacсount";
    public string Description { get; } = "Привязать к игроку банк или сделать новый";
    public string Help { get; } = "addbankacсount <uid> <account#>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            return;
        }

        BankAccountComponent? account = null;

        if (!EntityUid.TryParse(args[0], out var targetUid) || !targetUid.Valid ||
            !_entityManager.HasComponent<ActorComponent>(targetUid))
        {
            shell.WriteError("Игрок не найден!");
            return;
        }

        var bankManagerSystem = _entityManager.System<BankManagerSystem>();

        if (args.Length >= 2)
        {
            if (!bankManagerSystem.IsBankAccountExists(args[1]))
            {
                shell.WriteError("Банковский аккаунт не существует");
                return;
            }

            account = bankManagerSystem.GetBankAccount(args[1]);
        }

        var economySystem = _entityManager.System<EconomySystem>();
        account = economySystem.AddPlayerBank(targetUid, account, true);
        if (account == null)
        {
            shell.WriteError("Ошибка! Не возможно создать или привязать банковский аккаунт!");
            return;
        }

        if (args.Length >= 3 && int.TryParse(args[2], out var banalce) && banalce != 0)
        {
            bankManagerSystem.TryInsertToBankAccount(account!.AccountNumber,
                new KeyValuePair<string, FixedPoint2>(account.CurrencyType, FixedPoint2.New(banalce)));
        }

        _adminLogger.Add(LogType.AdminMessage, LogImpact.Extreme,
            $"Admin {(shell.Player != null ? shell.Player.Name : "An administrator")} AddBankAcсount {_entityManager.ToPrettyString(targetUid)} #{account!.AccountNumber} balance: {account.Balance}");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.NetEntities(args[0], _entityManager),
                "Персонаж с кпк"),
            2 => CompletionResult.FromHintOptions(
                _entityManager.System<BankManagerSystem>().ActiveBankAccounts
                    .Select(x => new CompletionOption(x.Value.AccountNumber, x.Value.AccountName))
                , "Аккаунт №"),
            _ => CompletionResult.Empty
        };
    }
}
