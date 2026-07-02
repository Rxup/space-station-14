// ReSharper disable once CheckNamespace

using Content.Server.Access.Systems;
using Content.Server.Backmen.Economy;
using Content.Server.Popups;
using Content.Server.VendingMachines;
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
        foreach (var currency in listing.Cost)
        {
            if (!component.Balance.TryGetValue(currency.Key, out var balance))
                return false;

            var fromMachine = FixedPoint2.Min(balance, currency.Value);
            var fromBank = currency.Value - fromMachine;

            if (fromMachine > FixedPoint2.Zero)
                component.Balance[currency.Key] -= fromMachine;

            if (fromBank <= FixedPoint2.Zero)
                continue;

            if (!_idCardSystem.TryFindIdCard(buyer, out var idCardComponent))
            {
                if (fromMachine > FixedPoint2.Zero)
                    component.Balance[currency.Key] += fromMachine;

                _PlayDeny(uid);
                _popup.PopupEntity(Loc.GetString("store-no-idcard"), uid);
                return false;
            }

            if (!_bankManagerSystem.TryWithdrawFromBankAccount(
                    idCardComponent.Owner,
                    new KeyValuePair<ProtoId<CurrencyPrototype>, FixedPoint2>(currency.Key, fromBank),
                    null))
            {
                if (fromMachine > FixedPoint2.Zero)
                    component.Balance[currency.Key] += fromMachine;

                _PlayDeny(uid);
                _popup.PopupEntity(Loc.GetString("store-no-money"), uid);
                return false;
            }
        }
        // end-backmen: vending-payment

        return true;
    }
}
