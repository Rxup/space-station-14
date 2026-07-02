// ReSharper disable once CheckNamespace

using System.Linq;
using Content.Server.Access.Systems;
using Content.Server.Backmen.Economy;
using Content.Server.Popups;
using Content.Server.VendingMachines;
using Content.Shared.Backmen.Economy;
using Content.Shared.Backmen.Store;
using Content.Shared.FixedPoint; // backmen: vending-payment
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared.VendingMachines;
using Robust.Shared.Prototypes; // backmen: vending-payment

namespace Content.Server.Store.Systems;

public sealed partial class StoreSystem
{
    [Dependency] private BankManagerSystem _bankManagerSystem = default!;
    [Dependency] private IdCardSystem _idCardSystem = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private VendingMachineSystem _vendingMachineSystem = default!;

    private readonly record struct StoreCurrencyDebit(
        ProtoId<CurrencyPrototype> Currency,
        FixedPoint2 FromMachine,
        FixedPoint2 FromBank);

    private void _PlayDeny(EntityUid uid)
    {
        if (TryComp<VendingMachineComponent>(uid, out var vendingMachineComponent))
        {
            _vendingMachineSystem.Deny((uid,vendingMachineComponent));
        }
    }
    private void _PlayEject(EntityUid uid)
    {
        if (TryComp<VendingMachineComponent>(uid, out var vendComponent))
        {
            vendComponent.NextItemToEject = null;
            vendComponent.ThrowNextItem = false;
            _vendingMachineSystem.TryUpdateVisualState((uid, vendComponent));
            _audio.PlayPvs(vendComponent.SoundVend, uid);
        }
    }
    private bool HandleBankTransaction(EntityUid uid, StoreComponent component, StoreBuyListingMessage msg, ListingDataWithCostModifiers listing)
    {
        if (!TryComp<BuyStoreBankComponent>(uid, out var storeBank))
        {
            return false;
        }

        if (msg.Actor is not { Valid: true } buyer)
            return false;

        // start-backmen: vending-payment
        var plan = new List<StoreCurrencyDebit>(listing.Cost.Count);
        foreach (var currency in listing.Cost)
        {
            if (!component.Balance.TryGetValue(currency.Key, out var balance))
                return false;

            var fromMachine = FixedPoint2.Min(balance, currency.Value);
            plan.Add(new StoreCurrencyDebit(currency.Key, fromMachine, currency.Value - fromMachine));
        }

        Entity<BankAccountComponent> bankAccount = default;
        if (plan.Any(debit => debit.FromBank > FixedPoint2.Zero))
        {
            if (!_idCardSystem.TryFindIdCard(buyer, out var idCard))
            {
                _PlayDeny(uid);
                _popup.PopupEntity(Loc.GetString("store-no-idcard"), uid);
                return false;
            }

            if (!_bankManagerSystem.TryGetBankAccount(idCard.Owner, out var bankAccountEnt))
            {
                _PlayDeny(uid);
                _popup.PopupEntity(Loc.GetString("store-no-money"), uid);
                return false;
            }

            bankAccount = bankAccountEnt.Value;
            var bank = bankAccount.Comp;

            foreach (var debit in plan)
            {
                if (debit.FromBank <= FixedPoint2.Zero)
                    continue;

                if (debit.Currency != bank.CurrencyType
                    || (!bank.IsInfinite && bank.Balance < debit.FromBank))
                {
                    _PlayDeny(uid);
                    _popup.PopupEntity(Loc.GetString("store-no-money"), uid);
                    return false;
                }
            }
        }

        var machineRollbacks = new List<(ProtoId<CurrencyPrototype> Currency, FixedPoint2 Amount)>();
        var bankRollbacks = new List<(Entity<BankAccountComponent> Account, ProtoId<CurrencyPrototype> Currency, FixedPoint2 Amount)>();

        foreach (var debit in plan)
        {
            if (debit.FromMachine > FixedPoint2.Zero)
            {
                component.Balance[debit.Currency] -= debit.FromMachine;
                machineRollbacks.Add((debit.Currency, debit.FromMachine));
            }

            if (debit.FromBank <= FixedPoint2.Zero)
                continue;

            if (!_bankManagerSystem.TryWithdrawFromBankAccount(
                    bankAccount,
                    new KeyValuePair<ProtoId<CurrencyPrototype>, FixedPoint2>(debit.Currency, debit.FromBank)))
            {
                RollbackStorePayment(component, machineRollbacks, bankRollbacks);
                _PlayDeny(uid);
                _popup.PopupEntity(Loc.GetString("store-no-money"), uid);
                return false;
            }

            bankRollbacks.Add((bankAccount, debit.Currency, debit.FromBank));
        }
        // end-backmen: vending-payment

        return true;
    }

    // start-backmen: vending-payment
    private void RollbackStorePayment(
        StoreComponent component,
        List<(ProtoId<CurrencyPrototype> Currency, FixedPoint2 Amount)> machineRollbacks,
        List<(Entity<BankAccountComponent> Account, ProtoId<CurrencyPrototype> Currency, FixedPoint2 Amount)> bankRollbacks)
    {
        foreach (var (currency, amount) in machineRollbacks)
            component.Balance[currency] += amount;

        foreach (var (account, currency, amount) in bankRollbacks)
        {
            _bankManagerSystem.TryInsertToBankAccount(
                account,
                new KeyValuePair<ProtoId<CurrencyPrototype>, FixedPoint2>(currency, amount));
        }
    }
    // end-backmen: vending-payment
}
