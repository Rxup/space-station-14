using Content.Server.CartridgeLoader;
using Content.Server.PDA.Ringer;
using Content.Server.Popups;
using Content.Shared.Backmen.CartridgeLoader.Cartridges;
using Content.Shared.Backmen.Economy;
using Content.Shared.CartridgeLoader;
using Content.Shared.Popups;
using Content.Shared.Store;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.CartridgeLoader.Cartridges;

public sealed class BankCartridgeSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly CartridgeLoaderSystem? _cartridgeLoaderSystem = default!;
    [Dependency] private readonly RingerSystem _ringerSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly ContainerSystem _containerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BankCartridgeComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<BankCartridgeComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<BankAccountComponent, ChangeBankAccountBalanceEvent>(OnChangeBankBalance);
        SubscribeLocalEvent<BankAccountComponent, EntGotInsertedIntoContainerMessage>(OnItemInserted);
        SubscribeLocalEvent<BankAccountComponent, EntGotRemovedFromContainerMessage>(OnItemRemoved);
    }

    private void OnItemRemoved(EntityUid uid, BankAccountComponent component, EntGotRemovedFromContainerMessage args)
    {
        if (
            !TryComp<CartridgeLoaderComponent>(args.Container.Owner, out var cartridgeLoaderComponent))
        {
            return;
        }

        _cartridgeLoaderSystem?.UpdateCartridgeUiState(args.Container.Owner, new BankUiState(component.Balance));
    }

    private void OnItemInserted(EntityUid uid, BankAccountComponent component, EntGotInsertedIntoContainerMessage args)
    {
        if (
            !TryComp<CartridgeLoaderComponent>(args.Container.Owner, out var cartridgeLoaderComponent))
        {
            return;
        }

        _cartridgeLoaderSystem?.UpdateCartridgeUiState(args.Container.Owner, new BankUiState(component.Balance));
    }

    private void OnComponentInit(EntityUid uid, BankCartridgeComponent bankCartrdigeComponent, ComponentInit args)
    {
    }

    private void OnComponentRemove(EntityUid uid, BankCartridgeComponent bankCartrdigeComponent, ComponentRemove args)
    {
        UnlinkBankAccountFromCartridge(uid, null, bankCartrdigeComponent);
    }

    public void LinkBankAccountToCartridge(EntityUid uid, BankAccountComponent bankAccount,
        BankCartridgeComponent? bankCartrdigeComponent = null)
    {
        if (!Resolve(uid, ref bankCartrdigeComponent))
        {
            return;
        }

        bankCartrdigeComponent.LinkedBankAccount = bankAccount;
        //bankAccount.BankCartridge = uid;
    }

    public void UnlinkBankAccountFromCartridge(EntityUid uid, BankAccountComponent? bankAccount = null,
        BankCartridgeComponent? bankCartrdigeComponent = null)
    {
        if (!Resolve(uid, ref bankCartrdigeComponent, false))
        {
            return;
        }

        bankCartrdigeComponent.LinkedBankAccount = null;
    }

    private void OnChangeBankBalance(EntityUid uid, BankAccountComponent component, ChangeBankAccountBalanceEvent args)
    {
        if ((MetaData(uid).Flags & MetaDataFlags.InContainer) == 0)
            return;
        var parent = Transform(uid).ParentUid;
        if (!parent.IsValid())
            return;

        if (TryComp<RingerComponent>(parent, out var ringerComponent))
        {
            _ringerSystem.RingerPlayRingtone(parent, ringerComponent);
            _cartridgeLoaderSystem?.UpdateCartridgeUiState(parent, new BankUiState(component.Balance));

            var player = Transform(parent).ParentUid;
            if (player.IsValid() && HasComp<ActorComponent>(player))
            {
                var currencySymbol = "";
                if (_prototypeManager.TryIndex(component.CurrencyType, out CurrencyPrototype? p))
                    currencySymbol = Loc.GetString(p.CurrencySymbol);

                var change = (double)(args.ChangeAmount ?? 0);
                var changeAmount = $"{change}";
                switch (change)
                {
                    case > 0:
                    {
                        changeAmount = $"+{change}";
                        break;
                    }
                    case < 0:
                    {
                        changeAmount = $"-{change}";
                        break;
                    }
                }

                _popupSystem.PopupEntity(
                    Loc.GetString(
                        "bank-program-change-balance-notification",
                        ("balance", component.Balance), ("change", changeAmount),
                        ( "currencySymbol", currencySymbol )
                    ),
                    parent,
                    Filter.Entities(player),
                    true,
                    PopupType.Medium
                );
            }
        }
        //UpdateUiState(uid, parent, component);
    }
}
