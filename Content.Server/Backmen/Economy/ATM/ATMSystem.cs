using System.Linq;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Stack;
using Content.Server.Store.Components;
using Content.Server.Store.Systems;
using Content.Server.UserInterface;
using Content.Shared.Access.Components;
using Content.Shared.Backmen.Economy;
using Content.Shared.Backmen.Economy.ATM;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Store;
using Content.Shared.Wires;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Economy.ATM;

    public sealed class ATMSystem : SharedATMSystem
    {
        [Dependency] private readonly IPrototypeManager _proto = default!;
        [Dependency] private readonly IEntityManager _entities = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly AppearanceSystem _appearanceSystem = default!;
        [Dependency] private readonly BankManagerSystem _bankManagerSystem = default!;
        [Dependency] private readonly StackSystem _stack = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;
        [Dependency] private readonly AudioSystem _audioSystem = default!;
        [Dependency] private readonly StoreSystem _storeSystem = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
        [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<AtmComponent, PowerChangedEvent>(OnPowerChanged);
            SubscribeLocalEvent<AtmComponent, ComponentStartup>((uid, comp, _) => UpdateComponentUserInterface(uid,comp));
            SubscribeLocalEvent<AtmComponent, EntInsertedIntoContainerMessage>((uid, comp, _) => UpdateComponentUserInterface(uid,comp));
            SubscribeLocalEvent<AtmComponent, EntRemovedFromContainerMessage>((uid, comp, _) => UpdateComponentUserInterface(uid,comp));
            SubscribeLocalEvent<AtmComponent, ATMRequestWithdrawMessage>(OnRequestWithdraw);
            SubscribeLocalEvent<AtmCurrencyComponent,AfterInteractEvent>(OnAfterInteract, before: new[]{typeof(StoreSystem)});
            SubscribeLocalEvent<AtmComponent, AfterActivatableUIOpenEvent>(OnInteract);
        }

        private void OnInteract(EntityUid uid, AtmComponent component, AfterActivatableUIOpenEvent args)
        {
            if (!this.IsPowered(uid, EntityManager))
                return;

            UpdateComponentUserInterface(uid,component);
        }

        public Dictionary<string, FixedPoint2> GetCurrencyValue(EntityUid uid, PhysicalCompositionComponent component)
        {
            var amount = EntityManager.GetComponentOrNull<StackComponent>(uid)?.Count ?? 1;
            var rt = new Dictionary<string, FixedPoint2>();
            if (component.MaterialComposition.TryGetValue("Credit", out var value))
            {
                rt.Add("SpaceCash", value * (FixedPoint2)amount);
            }
            return rt;
        }

        private void OnAfterInteract(EntityUid uid, AtmCurrencyComponent _, AfterInteractEvent args)
        {
            if (args.Handled || !args.CanReach)
                return;

            if (args.Target == null || !TryComp<PhysicalCompositionComponent>(args.Used, out var component) || !TryComp<AtmComponent>(args.Target, out var store))
                return;

            var user = args.User;

            args.Handled = TryAddCurrency(GetCurrencyValue(args.Used, component), args.Target.Value, store);

            if (args.Handled)
            {
                var msg = Loc.GetString("store-currency-inserted", ("used", args.Used), ("target", args.Target));
                _popup.PopupEntity(msg, args.Target.Value);
                Del(args.Used);
            }
        }

        private void OnPowerChanged(EntityUid uid, AtmComponent component, ref PowerChangedEvent args)
        {
            TryUpdateVisualState(uid, component);
        }
        public void TryUpdateVisualState(EntityUid uid, AtmComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;

            var finalState = ATMVisualState.Normal;
            if (!this.IsPowered(uid, EntityManager))
            {
                finalState = ATMVisualState.Off;
            }
            if (TryComp<AppearanceComponent>(uid, out var appearance))
            {
                _appearanceSystem.SetData(uid, ATMVisuals.VisualState, finalState, appearance);
            }
        }
        public void UpdateUi(EntityUid uid, BankAccountComponent bankAccount)
        {
            if (!_uiSystem.TryGetUi(uid, ATMUiKey.Key, out var ui))
                return;

            var currencySymbol = "";
            if(_prototypeManager.TryIndex(bankAccount.CurrencyType, out CurrencyPrototype? p))
                currencySymbol = Loc.GetString(p.CurrencySymbol);

            _uiSystem.SetUiState(ui,new AtmBoundUserInterfaceBalanceState(
                bankAccount.Balance,
                currencySymbol
            ));
        }
        private void UpdateComponentUserInterface(EntityUid uid, AtmComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;

            string? idCardFullName = null;
            string? idCardEntityName = null;
            string? idCardStoredBankAccountNumber = null;
            var haveAccessToBankAccount = false;
            FixedPoint2? bankAccountBalance = null;
            var currencySymbol = string.Empty;
            if (component.IdCardSlot.Item is { Valid: true } idCardEntityUid)
            {
                if (_entities.TryGetComponent<IdCardComponent>(idCardEntityUid, out var idCardComponent))
                {
                    idCardFullName = idCardComponent.FullName;
                    if (_bankManagerSystem.TryGetBankAccount(idCardEntityUid, out var bankAccount))
                    {
                        haveAccessToBankAccount = true;
                        bankAccountBalance = bankAccount.Balance;
                        if(_prototypeManager.TryIndex(bankAccount.CurrencyType, out CurrencyPrototype? p))
                            currencySymbol = Loc.GetString(p.CurrencySymbol);
                    }
                }
                idCardEntityName = MetaData(idCardEntityUid).EntityName;
            }

            var ui = _uiSystem.GetUiOrNull(uid, ATMUiKey.Key);
            if (ui == null)
                return;

            _uiSystem.SetUiState(ui,new AtmBoundUserInterfaceState(
                component.IdCardSlot.HasItem,
                idCardFullName,
                idCardEntityName,
                idCardStoredBankAccountNumber,
                haveAccessToBankAccount,
                bankAccountBalance,
                currencySymbol
            ));
        }
        private void OnRequestWithdraw(EntityUid uid, AtmComponent component, ATMRequestWithdrawMessage msg)
        {
            if (msg.Session.AttachedEntity is not { Valid: true } buyer)
                return;
            if (msg.Amount <= 0)
            {
                Deny(uid, component);
                return;
            }
            if (!TryGetBankAccountNumberFromStoredIdCard(component, out var bankAccountNumber))
            {
                Deny(uid, component);
                return;
            }
            if (component.CurrencyWhitelist.Count == 0)
            {
                Deny(uid, component);
                return;
            }
            var currency = component.CurrencyWhitelist.First();
            if (!_proto.TryIndex<CurrencyPrototype>(currency, out var proto))
            {
                Deny(uid, component);
                return;
            }
            if (proto.Cash == null || !proto.CanWithdraw)
            {
                Deny(uid, component);
                return;
            }

            var amountRemaining = msg.Amount;
            if (!_bankManagerSystem.TryWithdrawFromBankAccount(
                bankAccountNumber, msg.AccountPin,
                new KeyValuePair<string, FixedPoint2>(currency, amountRemaining)))
            {
                Deny(uid, component);
                return;
            }

            //FixedPoint2 amountRemaining = msg.Amount;
            var coordinates = Transform(buyer).Coordinates;
            var sortedCashValues = proto.Cash.Keys.OrderByDescending(x => x).ToList();
            foreach (var value in sortedCashValues)
            {
                var cashId = proto.Cash[value];
                var amountToSpawn = (int) MathF.Floor((float) (amountRemaining / value));
                var ents = _stack.SpawnMultiple(cashId, amountToSpawn, coordinates);
                _hands.PickupOrDrop(buyer, ents.First());
                amountRemaining -= value * amountToSpawn;
            }
            Apply(uid, component);
            _audioSystem.PlayPvs(component.SoundWithdrawCurrency, uid, AudioParams.Default.WithVolume(-2f));
            UpdateComponentUserInterface(uid, component);
        }
        public bool TryAddCurrency(Dictionary<string, FixedPoint2> currency, EntityUid atm, AtmComponent? component = null)
        {
            if (!Resolve(atm,ref component))
            {
                return false;
            }
            foreach (var type in currency)
            {
                if (!component.CurrencyWhitelist.Contains(type.Key))
                    return false;
            }
            if (!TryGetBankAccountNumberFromStoredIdCard(component, out var bankAccountNumber))
                return false;

            foreach (var type in currency)
            {
                if (!_bankManagerSystem.TryInsertToBankAccount(bankAccountNumber, type))
                    return false;
            }
            _audioSystem.PlayPvs(component.SoundInsertCurrency, atm, AudioParams.Default.WithVolume(-2f));
            UpdateComponentUserInterface(atm, component);
            return true;
        }
        private bool TryGetBankAccountNumberFromStoredIdCard(AtmComponent component, out string storedBankAccountNumber)
        {
            storedBankAccountNumber = string.Empty;
            if (component.IdCardSlot.Item is not { Valid: true } idCardEntityUid)
                return false;
            if (!_entities.TryGetComponent<IdCardComponent>(idCardEntityUid, out var idCardComponent))
                return false;
            if (idCardComponent.StoredBankAccountNumber == null)
                return false;
            storedBankAccountNumber = idCardComponent.StoredBankAccountNumber;
            return true;
        }
        private void Deny(EntityUid atm, AtmComponent? component = null)
        {
            if (!Resolve(atm,ref component))
            {
                return;
            }
            _audioSystem.PlayPvs(component.SoundDeny, atm, AudioParams.Default.WithVolume(-2f));
        }
        private void Apply(EntityUid atm, AtmComponent? component = null)
        {
            if (!Resolve(atm,ref component))
            {
                return;
            }
            _audioSystem.PlayPvs(component.SoundApply, atm, AudioParams.Default.WithVolume(-2f));
        }
    }
