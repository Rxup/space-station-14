using System.Linq;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Stack;
using Content.Server.Store.Components;
using Content.Server.Store.Systems;
using Content.Server.UserInterface;
using Content.Shared.Access.Components;
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
            SubscribeLocalEvent<ATMComponent, PowerChangedEvent>(OnPowerChanged);
            SubscribeLocalEvent<ATMComponent, ComponentStartup>((_, comp, _) => UpdateComponentUserInterface(comp));
            SubscribeLocalEvent<ATMComponent, EntInsertedIntoContainerMessage>((_, comp, _) => UpdateComponentUserInterface(comp));
            SubscribeLocalEvent<ATMComponent, EntRemovedFromContainerMessage>((_, comp, _) => UpdateComponentUserInterface(comp));
            SubscribeLocalEvent<ATMComponent, ATMRequestWithdrawMessage>(OnRequestWithdraw);
            SubscribeLocalEvent<AtmCurrencyComponent,AfterInteractEvent>(OnAfterInteract, before: new[]{typeof(StoreSystem)});
            SubscribeLocalEvent<ATMComponent, AfterActivatableUIOpenEvent>(OnInteract);
        }

        private void OnInteract(EntityUid uid, ATMComponent component, AfterActivatableUIOpenEvent args)
        {
            if (!this.IsPowered(uid, EntityManager))
                return;

            UpdateComponentUserInterface(component);
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

            if (args.Target == null || !TryComp<PhysicalCompositionComponent>(args.Used, out var component) || !TryComp<ATMComponent>(args.Target, out var store))
                return;

            var user = args.User;

            args.Handled = TryAddCurrency(GetCurrencyValue(uid, component), store);

            if (args.Handled)
            {
                var msg = Loc.GetString("store-currency-inserted", ("used", args.Used), ("target", args.Target));
                _popup.PopupEntity(msg, args.Target.Value);
                QueueDel(args.Used);
            }
        }

        private void OnPowerChanged(EntityUid uid, ATMComponent component, ref PowerChangedEvent args)
        {
            TryUpdateVisualState(uid, component);
        }
        public void TryUpdateVisualState(EntityUid uid, ATMComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;

            var finalState = ATMVisualState.Normal;
            if (!this.IsPowered(uid, EntityManager))
            {
                finalState = ATMVisualState.Off;
            }
            if (TryComp<AppearanceComponent>(component.Owner, out var appearance))
            {
                _appearanceSystem.SetData(uid, ATMVisuals.VisualState, finalState, appearance);
            }
        }
        private void UpdateComponentUserInterface(ATMComponent component)
        {
            string? idCardFullName = null;
            string? idCardEntityName = null;
            string? idCardStoredBankAccountNumber = null;
            bool haveAccessToBankAccount = false;
            FixedPoint2? bankAccountBalance = null;
            string currencySymbol = string.Empty;
            if (component.IdCardSlot.Item is { Valid: true } idCardEntityUid)
            {
                if (_entities.TryGetComponent<IdCardComponent>(idCardEntityUid, out var idCardComponent))
                {
                    idCardFullName = idCardComponent.FullName;
                    if (_bankManagerSystem.TryGetBankAccountWithPin(idCardComponent.StoredBankAccountNumber, idCardComponent.StoredBankAccountPin, out var bankAccount))
                    {
                        idCardStoredBankAccountNumber = idCardComponent.StoredBankAccountNumber;
                        if (bankAccount.AccountPin.Equals(idCardComponent.StoredBankAccountPin))
                        {
                            haveAccessToBankAccount = true;
                            bankAccountBalance = bankAccount.Balance;
                            if(bankAccount.CurrencyType != null && _prototypeManager.TryIndex(bankAccount.CurrencyType, out CurrencyPrototype? p))
                                currencySymbol = Loc.GetString(p.CurrencySymbol);
                        }
                    }
                }
                idCardEntityName = _entities.GetComponent<MetaDataComponent>(idCardEntityUid)?.EntityName;
            }
            var newState = new SharedATMComponent.ATMBoundUserInterfaceState(
                component.IdCardSlot.HasItem,
                idCardFullName,
                idCardEntityName,
                idCardStoredBankAccountNumber,
                haveAccessToBankAccount,
                bankAccountBalance,
                currencySymbol
                );

            var ui = _uiSystem.GetUiOrNull(component.Owner, ATMUiKey.Key);
            if (ui == null)
                return;
            UserInterfaceSystem.SetUiState(ui,newState);
        }
        private void OnRequestWithdraw(EntityUid uid, ATMComponent component, ATMRequestWithdrawMessage msg)
        {
            if (msg.Session.AttachedEntity is not { Valid: true } buyer)
                return;
            if (msg.Amount <= 0)
            {
                Deny(component);
                return;
            }
            if (!TryGetBankAccountNumberFromStoredIdCard(component, out var bankAccountNumber))
            {
                Deny(component);
                return;
            }
            if (component.CurrencyWhitelist.Count == 0)
            {
                Deny(component);
                return;
            }
            var currency = component.CurrencyWhitelist.First();
            if (!_proto.TryIndex<CurrencyPrototype>(currency, out var proto))
            {
                Deny(component);
                return;
            }
            if (proto.Cash == null || !proto.CanWithdraw)
            {
                Deny(component);
                return;
            }

            var amountRemaining = msg.Amount;
            if (!_bankManagerSystem.TryWithdrawFromBankAccount(
                bankAccountNumber, msg.AccountPin,
                new KeyValuePair<string, FixedPoint2>(currency, amountRemaining)))
            {
                Deny(component);
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
            Apply(component);
            _audioSystem.PlayPvs(component.SoundWithdrawCurrency, component.Owner, AudioParams.Default.WithVolume(-2f));
            UpdateComponentUserInterface(component);
        }
        public bool TryAddCurrency(Dictionary<string, FixedPoint2> currency, ATMComponent component)
        {
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
            _audioSystem.PlayPvs(component.SoundInsertCurrency, component.Owner, AudioParams.Default.WithVolume(-2f));
            UpdateComponentUserInterface(component);
            return true;
        }
        private bool TryGetBankAccountNumberFromStoredIdCard(ATMComponent component, out string storedBankAccountNumber)
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
        private void Deny(ATMComponent component)
        {
            _audioSystem.PlayPvs(component.SoundDeny, component.Owner, AudioParams.Default.WithVolume(-2f));
        }
        private void Apply(ATMComponent component)
        {
            _audioSystem.PlayPvs(component.SoundApply, component.Owner, AudioParams.Default.WithVolume(-2f));
        }
    }
