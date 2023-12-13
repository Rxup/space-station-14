using System.Diagnostics.CodeAnalysis;
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
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

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

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<AtmComponent, PowerChangedEvent>(OnPowerChanged);
            SubscribeLocalEvent<AtmComponent, ComponentStartup>((Entity<AtmComponent> uid, ref ComponentStartup _) => UpdateComponentUserInterface(uid));
            SubscribeLocalEvent<AtmComponent, EntInsertedIntoContainerMessage>((Entity<AtmComponent> uid, ref EntInsertedIntoContainerMessage _) => UpdateComponentUserInterface(uid));
            SubscribeLocalEvent<AtmComponent, EntRemovedFromContainerMessage>((Entity<AtmComponent> uid, ref EntRemovedFromContainerMessage _) => UpdateComponentUserInterface(uid));
            SubscribeLocalEvent<AtmComponent, ATMRequestWithdrawMessage>(OnRequestWithdraw);
            SubscribeLocalEvent<AtmCurrencyComponent,AfterInteractEvent>(OnAfterInteract, before: new[]{typeof(StoreSystem)});
            SubscribeLocalEvent<AtmComponent, AfterActivatableUIOpenEvent>(OnInteract);
        }

        private void OnInteract(Entity<AtmComponent> uid, ref AfterActivatableUIOpenEvent args)
        {
            if (!this.IsPowered(uid, EntityManager))
                return;

            UpdateComponentUserInterface(uid);
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

            if (args.Target == null || !TryComp<PhysicalCompositionComponent>(args.Used, out var component))
                return;

            if (TryComp<AtmComponent>(args.Target, out var atmComponent))
            {
                args.Handled = TryAddCurrency(GetCurrencyValue(args.Used, component), (args.Target.Value, atmComponent));
            }
            else if (TryComp<StoreComponent>(args.Target, out var store))
            {
                args.Handled = _storeSystem.TryAddCurrency(GetCurrencyValue(args.Used, component), args.Target.Value, store);
            }

            if (!args.Handled)
                return;

            var msg = Loc.GetString("store-currency-inserted", ("used", args.Used), ("target", args.Target));
            _popup.PopupEntity(msg, args.Target.Value);
            Del(args.Used);
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
        private void UpdateComponentUserInterface(Entity<AtmComponent> uid)
        {
            string? idCardFullName = null;
            string? idCardEntityName = null;
            string? idCardStoredBankAccountNumber = null;
            var haveAccessToBankAccount = false;
            FixedPoint2? bankAccountBalance = null;
            var currencySymbol = string.Empty;
            if (uid.Comp.IdCardSlot.Item is { Valid: true } idCardEntityUid)
            {
                if (_entities.TryGetComponent<IdCardComponent>(idCardEntityUid, out var idCardComponent))
                {
                    idCardFullName = idCardComponent.FullName;
                    if (!_bankManagerSystem.TryGetBankAccount(idCardEntityUid, out var bankAccount)) // новая карта (заведение счета в банке)
                    {
                        bankAccount = _bankManagerSystem.CreateNewBankAccount(idCardEntityUid);
                        DebugTools.Assert(bankAccount != null);
                        bankAccount.Value.Comp.AccountName = idCardFullName;
                        idCardComponent.StoredBankAccountNumber = bankAccount.Value.Comp.AccountNumber;
                        Dirty(idCardEntityUid, idCardComponent);
                        Dirty(bankAccount.Value);
                    }
                    haveAccessToBankAccount = true;
                    bankAccountBalance = bankAccount.Value.Comp.Balance;
                    if(_prototypeManager.TryIndex(bankAccount.Value.Comp.CurrencyType, out CurrencyPrototype? p))
                        currencySymbol = Loc.GetString(p.CurrencySymbol);
                }
                idCardEntityName = MetaData(idCardEntityUid).EntityName;
            }

            var ui = _uiSystem.GetUiOrNull(uid, ATMUiKey.Key);
            if (ui == null)
                return;

            _uiSystem.SetUiState(ui,new AtmBoundUserInterfaceState(
                uid.Comp.IdCardSlot.HasItem,
                idCardFullName,
                idCardEntityName,
                idCardStoredBankAccountNumber,
                haveAccessToBankAccount,
                bankAccountBalance,
                currencySymbol
            ));
        }
        private void OnRequestWithdraw(Entity<AtmComponent> uid, ref ATMRequestWithdrawMessage msg)
        {
            if (msg.Session.AttachedEntity is not { Valid: true } buyer)
                return;
            if (msg.Amount <= 0)
            {
                Deny(uid);
                return;
            }
            if (!TryGetBankAccountNumberFromStoredIdCard(uid, out var bankAccountNumber))
            {
                Deny(uid);
                return;
            }
            if (uid.Comp.CurrencyWhitelist.Count == 0)
            {
                Deny(uid);
                return;
            }
            var currency = uid.Comp.CurrencyWhitelist.First();
            if (!_proto.TryIndex<CurrencyPrototype>(currency, out var proto))
            {
                Deny(uid);
                return;
            }
            if (proto.Cash == null || !proto.CanWithdraw)
            {
                Deny(uid);
                return;
            }

            var amountRemaining = msg.Amount;
            if (!_bankManagerSystem.TryWithdrawFromBankAccount(
                bankAccountNumber,
                new KeyValuePair<string, FixedPoint2>(currency, amountRemaining)))
            {
                Deny(uid);
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
            Apply(uid);
            _audioSystem.PlayPvs(uid.Comp.SoundWithdrawCurrency, uid, AudioParams.Default.WithVolume(-2f));
            UpdateComponentUserInterface(uid);
        }
        public bool TryAddCurrency(Dictionary<string, FixedPoint2> currency, Entity<AtmComponent> atm)
        {
            foreach (var type in currency)
            {
                if (!atm.Comp.CurrencyWhitelist.Contains(type.Key))
                    return false;
            }
            if (!TryGetBankAccountNumberFromStoredIdCard(atm, out var bankAccountNumber))
                return false;

            foreach (var type in currency)
            {
                if (!_bankManagerSystem.TryInsertToBankAccount(bankAccountNumber, type))
                    return false;
            }
            _audioSystem.PlayPvs(atm.Comp.SoundInsertCurrency, atm, AudioParams.Default.WithVolume(-2f));
            UpdateComponentUserInterface(atm);
            return true;
        }
        private bool TryGetBankAccountNumberFromStoredIdCard(Entity<AtmComponent> component, [NotNullWhen(true)] out Entity<BankAccountComponent>? storedBankAccountNumber)
        {
            storedBankAccountNumber = null;
            if (component.Comp.IdCardSlot.Item is not { Valid: true } idCardEntityUid)
                return false;
            if (!HasComp<IdCardComponent>(idCardEntityUid))
                return false;

            return _bankManagerSystem.TryGetBankAccount(idCardEntityUid, out storedBankAccountNumber);
        }
        private void Deny(Entity<AtmComponent> atm)
        {
            _audioSystem.PlayPvs(atm.Comp.SoundDeny, atm, AudioParams.Default.WithVolume(-2f));
        }
        private void Apply(Entity<AtmComponent> atm)
        {
            _audioSystem.PlayPvs(atm.Comp.SoundApply, atm, AudioParams.Default.WithVolume(-2f));
        }
    }
