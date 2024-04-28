﻿// ReSharper disable once CheckNamespace

using Content.Server.Access.Systems;
using Content.Server.Backmen.Economy;
using Content.Server.Store.Components;
using Content.Server.VendingMachines;
using Content.Shared.Backmen.Store;
using Content.Shared.Store;
using Content.Shared.VendingMachines;

namespace Content.Server.Store.Systems;

public sealed partial class StoreSystem
{
    [Dependency] private readonly BankManagerSystem _bankManagerSystem = default!;
    [Dependency] private readonly IdCardSystem _idCardSystem = default!;
    [Dependency] private readonly VendingMachineSystem _vendingMachineSystem = default!;

    private void _PlayDeny(EntityUid uid)
    {
        if (TryComp<VendingMachineComponent>(uid, out var vendingMachineComponent))
        {
            _vendingMachineSystem.Deny(uid,vendingMachineComponent);
        }
    }
    private void _PlayEject(EntityUid uid)
    {
        if (TryComp<VendingMachineComponent>(uid, out var vendComponent))
        {
            vendComponent.Ejecting = true;
            vendComponent.NextItemToEject = null;
            vendComponent.ThrowNextItem = false;
            _vendingMachineSystem.TryUpdateVisualState(uid, vendComponent);
            _audio.PlayPvs(vendComponent.SoundVend, uid);
        }
    }
    private bool HandleBankTransaction(EntityUid uid, StoreComponent component, StoreBuyListingMessage msg, ListingData listing)
    {
        if (!TryComp<BuyStoreBankComponent>(uid, out var storeBank))
        {
            return false;
        }

        if (msg.Actor is not { Valid: true } buyer)
            return false;

        //check that we have enough money
        foreach (var currency in listing.Cost)
        {
            if (!component.Balance.TryGetValue(currency.Key, out var balance)) // || balance < currency.Value
            {
                return false;
            }

            if (balance >= currency.Value)
            {
                return false; // если уже достаточно валюты в автомате то нечего не делаем -_- (например рация ЯО, да-да-да, рация ЯО с покупкой с баланса банка, или баланс банка в ТК :))
            }

            if (!_idCardSystem.TryFindIdCard(buyer, out var idCardComponent))
            {
                _PlayDeny(uid);
                _popup.PopupEntity(Loc.GetString("store-no-idcard"),uid);
                return false;
            }

            if (!_bankManagerSystem.TryWithdrawFromBankAccount(idCardComponent.Owner, currency, null))
            {
                _PlayDeny(uid);
                _popup.PopupEntity(Loc.GetString("store-no-money"),uid);
                return false;
            }
        }

        return true; // успешно списано?
    }
}
