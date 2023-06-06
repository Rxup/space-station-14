using System.Diagnostics.CodeAnalysis;
using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Economy;

    public sealed class BankManagerSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _robustRandom = default!;
        [Dependency] private readonly IAdminLogManager _adminLogger = default!;

        [ViewVariables]
        public Dictionary<string, BankAccountComponent> ActiveBankAccounts = new();
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        }

        private void OnCleanup(RoundRestartCleanupEvent ev)
        {
            Clear();
        }

        public bool TryGetBankAccount(string? bankAccountNumber, [MaybeNullWhen(false)] out BankAccountComponent bankAccount)
        {
            bankAccount = GetBankAccount(bankAccountNumber);
            if (bankAccount == null || bankAccountNumber != bankAccount.AccountNumber)
                return false;
            return true;
        }
        public bool TryGetBankAccountWithPin(string? bankAccountNumber, string? bankAccountPin, [MaybeNullWhen(false)] out BankAccountComponent bankAccount)
        {
            bankAccount = null;
            if (bankAccountPin == null)
                return false;
            bankAccount = GetBankAccount(bankAccountNumber);
            if (bankAccount == null ||
                bankAccountNumber != bankAccount.AccountNumber ||
                bankAccountPin != bankAccount.AccountPin)
                return false;
            return true;
        }
        public BankAccountComponent? GetBankAccount(string? bankAccountNumber)
        {
            if (bankAccountNumber == null)
                return null;
            ActiveBankAccounts.TryGetValue(bankAccountNumber, out var bankAccount);
            return bankAccount;
        }
        public bool IsBankAccountExists(string? bankAccountNumber)
        {
            if (bankAccountNumber == null)
                return false;
            return ActiveBankAccounts.ContainsKey(bankAccountNumber);
        }
        public BankAccountComponent? CreateNewBankAccount(int? bankAccountNumber = null, bool _isInfinite = false)
        {
            int number;
            if(bankAccountNumber == null)
            {
                do
                {
                    number = _robustRandom.Next(111111, 999999);
                } while (ActiveBankAccounts.ContainsKey(number.ToString()));
            }
            else
            {
                number = (int) bankAccountNumber;
            }
            var bankAccountPin = GenerateBankAccountPin();
            var bankAccountNumberStr = number.ToString();
            var bankAccount = new BankAccountComponent(bankAccountNumberStr, bankAccountPin, isInfinite: _isInfinite);
            return ActiveBankAccounts.TryAdd(bankAccountNumberStr, bankAccount)
                ? bankAccount
                : null;
        }
        private string GenerateBankAccountPin()
        {
            var pin = string.Empty;
            for (var i = 0; i < 4; i++)
            {
                pin += _robustRandom.Next(0, 9).ToString();
            }
            return pin;
        }
        public bool TryWithdrawFromBankAccount(string? bankAccountNumber, string? bankAccountPin, KeyValuePair<string, FixedPoint2> currency)
        {
            if (!TryGetBankAccountWithPin(bankAccountNumber, bankAccountPin, out var bankAccount))
                return false;
            if (currency.Key != bankAccount.CurrencyType)
                return false;

            var oldBalance = bankAccount.Balance;
            var result = bankAccount.TryChangeBalanceBy(-currency.Value);
            if (result)
                _adminLogger.Add(
                    LogType.Transactions,
                    LogImpact.Low,
                    $"Account {bankAccount.AccountNumber} ({bankAccount.AccountName ?? "??"})  balance was changed by {-currency.Value}, from {oldBalance} to {bankAccount.Balance}");
            return result;
        }
        public bool TryInsertToBankAccount(string? bankAccountNumber, KeyValuePair<string, FixedPoint2> currency)
        {
            if (!TryGetBankAccount(bankAccountNumber, out var bankAccount))
                return false;
            if (currency.Key != bankAccount.CurrencyType)
                return false;

            var oldBalance = bankAccount.Balance;
            var result = bankAccount.TryChangeBalanceBy(currency.Value);
            if (result)
                _adminLogger.Add(
                    LogType.Transactions,
                    LogImpact.Low,
                    $"Account {bankAccount.AccountNumber} ({bankAccount.AccountName ?? "??"})  balance was changed by {-currency.Value}, from {oldBalance} to {bankAccount.Balance}");
            return result;
        }
        public bool TryTransferFromToBankAccount(string? bankAccountFromNumber, string? bankAccountFromPin, string? bankAccountToNumber, FixedPoint2 amount)
        {
            if (bankAccountFromNumber == null || bankAccountToNumber == null)
                return false;
            if (!TryGetBankAccountWithPin(bankAccountFromNumber, bankAccountFromPin, out var bankAccountFrom))
                return false;
            if (!ActiveBankAccounts.TryGetValue(bankAccountToNumber, out var bankAccountTo))
                return false;
            if (bankAccountFrom.CurrencyType != bankAccountTo.CurrencyType)
                return false;
            if (bankAccountFrom.TryChangeBalanceBy(-amount))
            {
                var result = bankAccountTo.TryChangeBalanceBy(amount);
                if (result)
                    _adminLogger.Add(
                        LogType.Transactions,
                        LogImpact.Low,
                        $"Account {bankAccountFrom.AccountNumber} ({bankAccountFrom.AccountName ?? "??"})  transfered {amount} to account {bankAccountTo.AccountNumber} ({bankAccountTo.AccountName ?? "??"})");
                return result;
            }
            return false;
        }
        public bool TryGetBankAccountCurrencyType(string? bankAccountNumber, out string? currencyType)
        {
            currencyType = null;
            if (bankAccountNumber == null)
                return false;
            if (!ActiveBankAccounts.TryGetValue(bankAccountNumber, out var bankAccount))
                return false;
            currencyType = bankAccount.CurrencyType;
            return true;
        }
        public string? GetBankAccountName(string? bankAccountNumber)
        {
            if (bankAccountNumber == null)
                return null;
            if (!ActiveBankAccounts.TryGetValue(bankAccountNumber, out var bankAccount))
                return null;
            return bankAccount.AccountName;
        }
        public void TryGenerateStartingBalance(BankAccountComponent bankAccount, JobPrototype jobPrototype)
        {
            if (jobPrototype.MaxBankBalance > 0)
            {
                var newBalance = FixedPoint2.New(_robustRandom.Next(jobPrototype.MinBankBalance, jobPrototype.MaxBankBalance));
                bankAccount.SetBalance(newBalance);
            }
        }
        public void Clear()
        {
            ActiveBankAccounts.Clear();
        }
    }
