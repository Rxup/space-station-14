using Content.Server.Backmen.Economy;
using Content.Server.CartridgeLoader;
using Content.Server.PDA.Ringer;
using Content.Server.Popups;
using Content.Shared.Backmen.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;
using Content.Shared.Store;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.CartridgeLoader.Cartridges;

public sealed class BankCartridgeSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly CartridgeLoaderSystem? _cartridgeLoaderSystem = default!;
    [Dependency] private readonly RingerSystem _ringerSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BankCartridgeComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<BankCartridgeComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<BankCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<BankCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<BankCartridgeComponent, ChangeBankAccountBalanceEvent>(OnChangeBankBalance);
    }

    private void OnComponentInit(EntityUid uid, BankCartridgeComponent bankCartrdigeComponent, ComponentInit args)
    {

    }
    private void OnComponentRemove(EntityUid uid, BankCartridgeComponent bankCartrdigeComponent, ComponentRemove args)
    {
        UnlinkBankAccountFromCartridge(bankCartrdigeComponent);
    }
    public void LinkBankAccountToCartridge(BankCartridgeComponent bankCartrdigeComponent, BankAccountComponent bankAccount)
    {
        bankCartrdigeComponent.LinkedBankAccount = bankAccount;
        bankAccount.BankCartridge = bankCartrdigeComponent.Owner;
    }
    public void UnlinkBankAccountFromCartridge(BankCartridgeComponent bankCartrdigeComponent, BankAccountComponent? bankAccount = null)
    {
        if (bankAccount == null)
            bankAccount = bankCartrdigeComponent.LinkedBankAccount;
        bankCartrdigeComponent.LinkedBankAccount = null;

        if (bankAccount != null)
            bankAccount.BankCartridge = null;
    }

    private void OnChangeBankBalance(EntityUid uid, BankCartridgeComponent component, ChangeBankAccountBalanceEvent args)
    {
        if ((MetaData(uid).Flags & MetaDataFlags.InContainer) == 0)
            return;
        var parent = Transform(uid).ParentUid;
        if (!parent.IsValid())
            return;

        if (HasComp<RingerComponent>(parent))
            EnsureComp<ActiveRingerComponent>(parent);
            //_ringerSystem.RingerPlayRingtonePublic(parent);
        //_popupSystem.PopupEntity(Loc.GetString("bank-program-change-balance-notification"), parent);

        UpdateUiState(uid, parent, component);
    }

    private void OnUiReady(EntityUid uid, BankCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        UpdateUiState(uid, args.Loader, component);
    }
    private void OnUiMessage(EntityUid uid, BankCartridgeComponent component, CartridgeMessageEvent args)
    {
        UpdateUiState(uid, args.LoaderUid, component);
    }
    private void UpdateUiState(EntityUid uid, EntityUid loaderUid, BankCartridgeComponent? component)
    {
        if (!Resolve(uid, ref component))
            return;

        var state = new BankUiState();
        if (component.LinkedBankAccount!= null)
        {
            state.LinkedAccountNumber = component.LinkedBankAccount.AccountNumber;
            state.LinkedAccountName = component.LinkedBankAccount.AccountName;
            state.LinkedAccountBalance = component.LinkedBankAccount.Balance;
            if (component.LinkedBankAccount.CurrencyType != null && _prototypeManager.TryIndex(component.LinkedBankAccount.CurrencyType, out CurrencyPrototype? p))
                state.CurrencySymbol = Loc.GetString(p.CurrencySymbol);
        }
        _cartridgeLoaderSystem?.UpdateCartridgeUiState(loaderUid, state);
    }
}
