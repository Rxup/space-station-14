using Content.Server.Backmen.Economy.ATM;
using Content.Server.Store.Components;
using Content.Server.Store.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Server.Backmen.Economy;

public sealed class EconomySystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StoreSystem _storeSystem = default!;
    [Dependency] private readonly ATMSystem _atmSystem = default!;
    public override void Initialize()
    {
        base.Initialize();
        // SubscribeLocalEvent<CurrencyComponent, AfterInteractEvent>(OnAfterInteract);
    }
    /*
    private void OnAfterInteract(EntityUid uid, CurrencyComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (args.Target == null)
            return;

        if (TryComp<ATMComponent>(args.Target, out var atm))
            args.Handled = _atmSystem.TryAddCurrency(_storeSystem.GetCurrencyValue(uid, component), atm);
        else if (TryComp<StoreComponent>(args.Target, out var store))
        {
            if (!store.Opened)
            {
                _storeSystem.RefreshAllListings(store);
                _storeSystem.InitializeFromPreset(store.Preset, store.AccountOwner, store);
                store.Opened = true;
            }
            args.Handled = _storeSystem.TryAddCurrency(_storeSystem.GetCurrencyValue(uid, component), uid, store);
        }
        else
            return;

        if (args.Handled)
        {
            var msg = Loc.GetString("store-currency-inserted", ("used", args.Used), ("target", args.Target));
            _popup.PopupEntity(msg, args.Target.Value);
            QueueDel(args.Used);
        }
    }
    */
}
