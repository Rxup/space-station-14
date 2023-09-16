using System.Diagnostics.CodeAnalysis;
using Content.Server.Administration.Logs;
using Content.Server.Backmen.CartridgeLoader.Cartridges;
using Content.Server.Backmen.Economy.ATM;
using Content.Shared.Backmen.Economy;
using Content.Shared.Backmen.Economy.ATM;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Economy;

    public sealed class BankManagerSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _robustRandom = default!;
        [Dependency] private readonly IAdminLogManager _adminLogger = default!;
        [Dependency] private readonly ATMSystem _atmSystem = default!;

        [ViewVariables] public readonly Dictionary<string, (EntityUid owner, BankAccountComponent account)> ActiveBankAccounts = new();

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
            SubscribeLocalEvent<BankAccountComponent, BankChangeBalanceEvent>(OnBalanceChange);
            SubscribeLocalEvent<BankAccountComponent, ComponentGetStateAttemptEvent>(IsCardInHand);
        }

        private void IsCardInHand(EntityUid uid, BankAccountComponent component, ref ComponentGetStateAttemptEvent args)
        {

        }

        private void OnBalanceChange(EntityUid uid, BankAccountComponent component, BankChangeBalanceEvent args)
        {
            if (args.Handled)
            {
                return;
            }
            args.Handled = true;

            if (component.IsInfinite)
            {
                return;
            }

            component.SetBalance(args.Balance);
            Dirty(uid, component);

            var ev = new ChangeBankAccountBalanceEvent(args.Balance - args.OldBalance, args.Balance);
            RaiseLocalEvent(uid, ev);

            var parent = Transform(uid).ParentUid;
            if (parent.IsValid() && HasComp<AtmComponent>(parent))
            {
                _atmSystem.UpdateUi(parent, component);
            }
        }

        public bool TryChangeBalanceBy(EntityUid uid, FixedPoint2 amount, BankAccountComponent? component = null)
        {
            if (!Resolve(uid, ref component))
            {
                return false;
            }

            if (component.Balance + amount < 0)
                return false;
            var oldBalance = component.Balance;

            var newBalance = component.Balance + amount;

            var ev = new BankChangeBalanceEvent()
            {
                OldBalance = oldBalance,
                Balance = newBalance
            };
            RaiseLocalEvent(uid, ev, true);

            return ev.Handled;
        }
        public bool TrySetBalance(EntityUid uid, FixedPoint2 amount, BankAccountComponent? component = null)
        {
            if (!Resolve(uid, ref component))
            {
                return false;
            }

            if (component.Balance + amount < 0)
                return false;
            var oldBalance = component.Balance;

            var ev = new BankChangeBalanceEvent()
            {
                OldBalance = oldBalance,
                Balance = amount
            };
            RaiseLocalEvent(uid, ev, true);

            return ev.Handled;
        }

        private void OnCleanup(RoundRestartCleanupEvent ev)
        {
            Clear();
        }

        public bool TryGetBankAccount(EntityUid? bankAccountOwner, [MaybeNullWhen(false)] out BankAccountComponent bankAccount)
        {
            return TryComp(bankAccountOwner, out bankAccount);
        }
        public bool TryGetBankAccount(string? bankAccountNumber, [MaybeNullWhen(false)] out  EntityUid bankAccountOwner, [MaybeNullWhen(false)] out BankAccountComponent bankAccount)
        {
            bankAccount = null;
            bankAccountOwner = EntityUid.Invalid;
            var bankInfo = GetBankAccount(bankAccountNumber);
            if (bankInfo == null || bankAccountNumber != bankInfo.Value.account.AccountNumber)
                return false;
            bankAccount = bankInfo!.Value.account;
            bankAccountOwner = bankInfo!.Value.owner;
            return true;
        }
        public bool TryGetBankAccountWithPin(string? bankAccountNumber, string? bankAccountPin, [MaybeNullWhen(false)] out  EntityUid bankAccountOwner, [MaybeNullWhen(false)] out BankAccountComponent bankAccount)
        {
            bankAccount = null;
            bankAccountOwner = EntityUid.Invalid;
            if (bankAccountPin == null)
                return false;
            var bankInfo = GetBankAccount(bankAccountNumber);
            if (bankInfo == null ||
                bankAccountNumber != bankInfo.Value.account.AccountNumber ||
                bankAccountPin != bankInfo.Value.account.AccountPin)
                return false;
            bankAccount = bankInfo!.Value.account;
            bankAccountOwner = bankInfo!.Value.owner;
            return true;
        }
        public (EntityUid owner,BankAccountComponent account)? GetBankAccount(string? bankAccountNumber)
        {
            if (bankAccountNumber == null)
                return null;
            ActiveBankAccounts.TryGetValue(bankAccountNumber, out var bankAccount);
            return bankAccount;
        }
        public bool IsBankAccountExists(string? bankAccountNumber)
        {
            return bankAccountNumber != null &&
                   ActiveBankAccounts.ContainsKey(bankAccountNumber) &&
                   !TerminatingOrDeleted(ActiveBankAccounts[bankAccountNumber].owner);
        }
        public BankAccountComponent? CreateNewBankAccount(EntityUid idCardId, int? bankAccountNumber = null, bool isInfinite = false)
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
            var bankAccount = EnsureComp<BankAccountComponent>(idCardId);
            bankAccount.AccountNumber = bankAccountNumberStr;
            bankAccount.AccountPin = bankAccountPin;
            bankAccount.IsInfinite = isInfinite;
            Dirty(idCardId,bankAccount);
            return ActiveBankAccounts.TryAdd(bankAccountNumberStr, (idCardId,bankAccount))
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

        public bool TryWithdrawFromBankAccount(string? bankAccountNumber, string? bankAccountPin,
            KeyValuePair<string, FixedPoint2> currency)
        {
            if (!TryGetBankAccountWithPin(bankAccountNumber, bankAccountPin, out var bankAccountOwner, out var bankAccount))
                return false;

            return TryWithdrawFromBankAccount(bankAccountOwner, currency, bankAccount);
        }

        public bool TryWithdrawFromBankAccount(EntityUid? bankAccountOwner, KeyValuePair<string, FixedPoint2> currency,
            BankAccountComponent bankAccount)
        {
            if (bankAccountOwner == null)
            {
                return false;
            }
            return TryWithdrawFromBankAccount(bankAccountOwner.Value, currency, bankAccount);
        }
        public bool TryWithdrawFromBankAccount(EntityUid bankAccountOwner, KeyValuePair<string, FixedPoint2> currency, BankAccountComponent bankAccount)
        {
            if (currency.Key != bankAccount.CurrencyType)
                return false;

            var oldBalance = bankAccount.Balance;
            var result = TryChangeBalanceBy(bankAccountOwner, -currency.Value, bankAccount);
            if (result)
            {
                _adminLogger.Add(
                    LogType.Transactions,
                    LogImpact.Low,
                    $"Account {bankAccount.AccountNumber} ({bankAccount.AccountName ?? "??"})  balance was changed by {-currency.Value}, from {oldBalance} to {bankAccount.Balance}");
            }

            return result;
        }
        public bool TryInsertToBankAccount(string? bankAccountNumber, KeyValuePair<string, FixedPoint2> currency)
        {
            if (!TryGetBankAccount(bankAccountNumber, out var bankAccountOwner, out var bankAccount))
                return false;

            if (!TryInsertToBankAccount(bankAccountOwner, currency, bankAccount))
                return false;

            return true;
        }

        public bool TryInsertToBankAccount(EntityUid? bankOwner, KeyValuePair<string, FixedPoint2> currency,
            BankAccountComponent? bankAccount = null)
        {
            if (bankOwner == null)
                return false;

            return TryInsertToBankAccount(bankOwner.Value, currency, bankAccount);
        }
        public bool TryInsertToBankAccount(EntityUid bankOwner, KeyValuePair<string, FixedPoint2> currency, BankAccountComponent? bankAccount = null)
        {
            if (!Resolve(bankOwner, ref bankAccount))
                return false;

            if (currency.Key != bankAccount.CurrencyType)
                return false;

            var oldBalance = bankAccount.Balance;
            var result = TryChangeBalanceBy(bankOwner, currency.Value, bankAccount);
            if (result)
            {
                _adminLogger.Add(
                    LogType.Transactions,
                    LogImpact.Low,
                    $"Account {bankAccount.AccountNumber} ({bankAccount.AccountName ?? "??"})  balance was changed by {-currency.Value}, from {oldBalance} to {bankAccount.Balance}");
            }

            return result;
        }
        public bool TryTransferFromToBankAccount(string? bankAccountFromNumber, string? bankAccountFromPin, string? bankAccountToNumber, FixedPoint2 amount)
        {
            if (bankAccountFromNumber == null || bankAccountToNumber == null)
                return false;
            if (!TryGetBankAccountWithPin(bankAccountFromNumber, bankAccountFromPin, out var bankAccountFromOwner, out var bankAccountFrom))
                return false;
            if (!ActiveBankAccounts.TryGetValue(bankAccountToNumber, out var bankInfo))
                return false;
            var (bankAccountToOwner, bankAccountTo) = bankInfo;
            if (bankAccountFrom.CurrencyType != bankAccountTo.CurrencyType)
                return false;
            if (TryChangeBalanceBy(bankAccountFromOwner, -amount, bankAccountFrom))
            {
                var result = TryChangeBalanceBy(bankAccountToOwner,amount,bankAccountTo);
                if (result)
                {
                    _adminLogger.Add(
                        LogType.Transactions,
                        LogImpact.Low,
                        $"Account {bankAccountFrom.AccountNumber} ({bankAccountFrom.AccountName ?? "??"})  transfered {amount} to account {bankAccountTo.AccountNumber} ({bankAccountTo.AccountName ?? "??"})");
                }
                else
                {
                    TryChangeBalanceBy(bankAccountFromOwner, amount, bankAccountFrom); // rollback
                }

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
            currencyType = bankAccount.account.CurrencyType;
            return true;
        }
        public string? GetBankAccountName(string? bankAccountNumber)
        {
            if (bankAccountNumber == null)
                return null;
            if (!ActiveBankAccounts.TryGetValue(bankAccountNumber, out var bankAccount))
                return null;
            return bankAccount.account.AccountName;
        }
        public void TryGenerateStartingBalance(BankAccountComponent bankAccount, JobPrototype jobPrototype)
        {
            if (jobPrototype.MaxBankBalance <= 0)
                return;

            var newBalance = FixedPoint2.New(_robustRandom.Next(jobPrototype.MinBankBalance, jobPrototype.MaxBankBalance));
            bankAccount.SetBalance(newBalance);
        }
        public void Clear()
        {
            ActiveBankAccounts.Clear();
        }
    }
