using Content.Server.Access.Systems;
using Content.Server.Popups;
using Content.Shared.Access.Components;
using Content.Shared.Backmen.Economy.Eftpos;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Store;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Economy.Eftpos;

    public sealed class EftposSystem : EntitySystem
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly BankManagerSystem _bankManagerSystem = default!;
        [Dependency] private readonly IdCardSystem _idCardSystem = default!;
        [Dependency] private readonly AudioSystem _audioSystem = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<IdCardComponent, AfterInteractEvent>(OnAfterInteract);
            SubscribeLocalEvent<EftposComponent, ComponentStartup>((uid, comp, _) => UpdateComponentUserInterface(uid,comp));
            SubscribeLocalEvent<EftposComponent, EftposChangeValueMessage>(OnChangeValue);
            SubscribeLocalEvent<EftposComponent, EftposChangeLinkedAccountNumberMessage>(OnChangeLinkedAccountNumber);
            SubscribeLocalEvent<EftposComponent, EftposSwipeCardMessage>(OnSwipeCard);
            SubscribeLocalEvent<EftposComponent, EftposLockMessage>(OnLock);
        }
        private void UpdateComponentUserInterface(EntityUid uid, EftposComponent component)
        {
            string? currSymbol = null;
            if (component.CurrencyType != null && _prototypeManager.TryIndex(component.CurrencyType, out CurrencyPrototype? p))
                currSymbol = Loc.GetString(p.CurrencySymbol);
            var newState = new SharedEftposComponent.EftposBoundUserInterfaceState(
                component.Value,
                component.LinkedAccountNumber,
                component.LinkedAccountName,
                component.LockedBy != null,
                currSymbol);

            if (!_uiSystem.TryGetUi(uid, EftposUiKey.Key, out var bui))
            {
                return;
            }
            _uiSystem.SetUiState(bui, newState);
        }
        private void OnChangeValue(EntityUid uid, EftposComponent component, EftposChangeValueMessage msg)
        {
            if (component.LockedBy != null)
            {
                Deny(component);
                return;
            }
            if (msg.Session.AttachedEntity is not { Valid: true } mob)
                return;
            component.Value =
                msg.Value != null
                ? FixedPoint2.Clamp((FixedPoint2) msg.Value, 0, FixedPoint2.MaxValue)
                : null;
            UpdateComponentUserInterface(uid,component);
        }
        private void OnChangeLinkedAccountNumber(EntityUid uid, EftposComponent component, EftposChangeLinkedAccountNumberMessage msg)
        {
            if (component.LockedBy != null || !component.CanChangeAccountNumber)
            {
                Deny(component);
                return;
            }
            if (msg.Session.AttachedEntity is not { Valid: true } mob)
                return;
            if (msg.LinkedAccountNumber == null)
            {
                component.CurrencyType = null;
                component.LinkedAccountNumber = null;
                component.LinkedAccountName = null;
                UpdateComponentUserInterface(uid,component);
                Apply(component);
                return;
            }
            if (!_bankManagerSystem.TryGetBankAccountCurrencyType(msg.LinkedAccountNumber, out var currencyType))
            {
                Deny(component);
                return;
            }
            component.CurrencyType = currencyType;
            component.LinkedAccountNumber = msg.LinkedAccountNumber;
            component.LinkedAccountName = _bankManagerSystem.GetBankAccountName(msg.LinkedAccountNumber);

            Apply(component);
            UpdateComponentUserInterface(uid,component);
        }
        private void OnSwipeCard(EntityUid uid, EftposComponent component, EftposSwipeCardMessage msg)
        {
            if (msg.Session.AttachedEntity is not { Valid: true } buyer)
                return;
            if (!_idCardSystem.TryFindIdCard(buyer, out var idCardComponent))
            {
                Deny(component);
                return;
            }
            TryCompleteTransaction(uid, component, idCardComponent);
        }
        private void OnAfterInteract(EntityUid uid, IdCardComponent component, AfterInteractEvent args)
        {
            if (!TryComp<EftposComponent>(args.Target, out var eftpos))
                return;
            TryCompleteTransaction(args.Target.Value, eftpos, component);
        }
        private void TryCompleteTransaction(EntityUid terminal, EftposComponent component, IdCardComponent idCardComponent)
        {
            if (idCardComponent.Owner == component.LockedBy)
            {
                component.LockedBy = null;
                UpdateComponentUserInterface(terminal, component);
                Apply(component);
                return;
            }
            if (component.Value == null)
                return;

            if (!_bankManagerSystem.TryTransferFromToBankAccount(
                idCardComponent.StoredBankAccountNumber,
                idCardComponent.StoredBankAccountPin,
                component.LinkedAccountNumber,
                (FixedPoint2) component.Value))
            {
                _popupSystem.PopupEntity(Loc.GetString("eftpos-ui-popup-deny-nomoney"),component.Owner, PopupType.LargeCaution);
                Deny(component);
                return;
            }
            _popupSystem.PopupEntity(Loc.GetString("eftpos-ui-popup-apply-done"),component.Owner, PopupType.Large);
            Apply(component);
            UpdateComponentUserInterface(terminal, component);
        }

        private void OnLock(EntityUid uid, EftposComponent component, EftposLockMessage msg)
        {
            if (msg.Session.AttachedEntity is not { Valid: true } buyer)
                return;
            if (component.LockedBy != null)
            {
                _popupSystem.PopupEntity(Loc.GetString("eftpos-ui-popup-deny-lock-already"),component.Owner, PopupType.SmallCaution);
                Deny(component);
                return;
            }
            if (component.LinkedAccountNumber == null || component.Value == null)
            {
                _popupSystem.PopupEntity(Loc.GetString("eftpos-ui-popup-deny-lock-invalid"), component.Owner,
                    PopupType.SmallCaution);
                Deny(component);
                return;
            }
            if (!_idCardSystem.TryFindIdCard(buyer, out var idCardComponent))
            {
                _popupSystem.PopupEntity(Loc.GetString("eftpos-ui-popup-deny-lock-noidcard"),component.Owner, PopupType.SmallCaution);
                Deny(component);
                return;
            }
            component.LockedBy = idCardComponent.Owner;
            _popupSystem.PopupEntity(Loc.GetString("eftpos-ui-popup-lock"),component.Owner, PopupType.Small);
            Apply(component);
            UpdateComponentUserInterface(uid, component);
        }

        private void Deny(EftposComponent component)
        {
            _audioSystem.PlayPvs(component.SoundDeny, component.Owner, AudioParams.Default.WithVolume(-2f));
        }
        private void Apply(EftposComponent component)
        {
            _audioSystem.PlayPvs(component.SoundApply, component.Owner, AudioParams.Default.WithVolume(-2f));
        }
    }
