//using Content.Server.Backmen.Economy.ATM;

using System.Linq;
using Content.Server.Access.Systems;
using Content.Server.Backmen.CartridgeLoader.Cartridges;
using Content.Server.Backmen.Economy.Eftpos;
using Content.Server.Backmen.Economy.Wage;
using Content.Server.Backmen.Mind;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared.Access.Components;
using Content.Shared.Backmen.Economy;
using Content.Shared.CartridgeLoader;
using Content.Shared.Inventory;
using Content.Shared.Objectives;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Economy;

public sealed class EconomySystem : EntitySystem
{
    [Dependency] private readonly BankManagerSystem _bankManagerSystem = default!;
    [Dependency] private readonly WageManagerSystem _wageManagerSystem = default!;
    [Dependency] private readonly BankCartridgeSystem _bankCartridgeSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly IdCardSystem _cardSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly BankManagerSystem _bankManager = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly RoleSystem _roleSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned, after: new []{ typeof(Corvax.Loadout.LoadoutSystem) });
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStartingEvent);
        SubscribeLocalEvent<EftposComponent, ComponentInit>(OnFtposInit);
        SubscribeLocalEvent<MindNoteConditionComponent, ObjectiveGetProgressEvent>(OnGetBankProgress);
        SubscribeLocalEvent<MindNoteConditionComponent, ObjectiveAfterAssignEvent>(OnAfterBankAssign);
    }

    private void OnGetBankProgress(EntityUid _, MindNoteConditionComponent component, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = 1;
    }

    private void OnAfterBankAssign(EntityUid uid, MindNoteConditionComponent component, ref ObjectiveAfterAssignEvent args)
    {
        if (!TryComp<BankMemoryComponent>(args.MindId, out var bankMemory))
        {
            UpdateNote(uid);
            return;
        }

        if (!TryComp<BankAccountComponent>(bankMemory.BankAccount, out var bankAccount))
        {
            UpdateNote(uid);
            return;
        }

        UpdateNote(uid, bankAccount);
    }

    private void UpdateNote(EntityUid uid, BankAccountComponent? bank = null)
    {
        _metaDataSystem.SetEntityName(uid, Loc.GetString("character-info-memories-placeholder-text"));
        _metaDataSystem.SetEntityDescription(uid, bank != null
            ? Loc.GetString("memory-account-number", ("value", bank!.AccountNumber)) + "\n" +
              Loc.GetString("memory-account-pin", ("value", bank!.AccountPin))
            : "");
        DirtyEntity(uid);
    }

    private void OnFtposInit(EntityUid uid, EftposComponent component, ComponentInit args)
    {
        component.InitPresetValues();
    }

    #region EventHandle

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        AddPlayerBank(ev.Mob);
    }
    private void OnRoundStartingEvent(RoundStartingEvent ev)
    {
        foreach (var department in _prototype.EnumeratePrototypes<DepartmentPrototype>())
        {
            var dummy = Spawn("CaptainIDCard");
            _metaDataSystem.SetEntityName(dummy,"Bank: "+department.AccountNumber);
            var bankAccount = _bankManagerSystem.CreateNewBankAccount(dummy, department.AccountNumber, true);
            if (bankAccount == null)
                continue;
            bankAccount.AccountName = department.ID;
            bankAccount.Balance = 100_000;
        }
    }

    #endregion

    #region PublicApi
    [PublicAPI]
    public bool TryStoreNewBankAccount(EntityUid player, EntityUid idCardId, IdCardComponent? id, out BankAccountComponent? bankAccount)
    {
        bankAccount = null;
        if (!Resolve(idCardId, ref id))
            return false;
        bankAccount = _bankManager.CreateNewBankAccount(idCardId);
        if (bankAccount == null)
            return false;
        id.StoredBankAccountNumber = bankAccount.AccountNumber;
        id.StoredBankAccountPin = bankAccount.AccountPin;
        bankAccount.AccountName = id.FullName;
        if (string.IsNullOrEmpty(bankAccount.AccountName))
        {
            bankAccount.AccountName = MetaData(player).EntityName;
        }
        Dirty(idCardId, bankAccount);
        return true;
    }

    private void AttachPdaBank(EntityUid player, BankAccountComponent bankAccount)
    {
        if (!_inventorySystem.TryGetSlotEntity(player, "id", out var idUid))
            return;

        if (!EntityManager.TryGetComponent(idUid, out CartridgeLoaderComponent? cartrdigeLoaderComponent))
            return;

        foreach (var uid in cartrdigeLoaderComponent.BackgroundPrograms)
        {
            if (!TryComp<BankCartridgeComponent>(uid, out var bankCartrdigeComponent))
                continue;

            if (bankCartrdigeComponent.LinkedBankAccount == null)
            {
                _bankCartridgeSystem.LinkBankAccountToCartridge(uid, bankAccount, bankCartrdigeComponent);
            }
            else if(bankCartrdigeComponent.LinkedBankAccount.AccountNumber != bankAccount.AccountNumber)
            {
                _bankCartridgeSystem.UnlinkBankAccountFromCartridge(uid, bankCartrdigeComponent.LinkedBankAccount, bankCartrdigeComponent);
                _bankCartridgeSystem.LinkBankAccountToCartridge(uid, bankAccount, bankCartrdigeComponent);
            }
            // else: do nothing
        }
    }

    [PublicAPI]
    public (EntityUid owner,BankAccountComponent account)? AddPlayerBank(EntityUid player, BankAccountComponent? bankAccount = null, bool AttachWage = true)
    {
        if (!_cardSystem.TryFindIdCard(player, out var idCardComponent))
            return null;

        if (!_mindSystem.TryGetMind(player, out var mindId, out var mind))
        {
            return null;
        }

        var idCardUid = idCardComponent.Owner;

        if (bankAccount == null)
        {
            if (!TryStoreNewBankAccount(player, idCardUid, idCardComponent, out bankAccount) || bankAccount == null)
            {
                return null;
            }

            if (AttachWage && !_roleSystem.MindHasRole<JobComponent>(mindId))
            {
                AttachWage = false;
            }

            if (TryComp<JobComponent>(mindId, out var jobComponent) && jobComponent.PrototypeId != null && _prototype.TryIndex<JobPrototype>(jobComponent.PrototypeId, out var jobPrototype))
            {
                _bankManagerSystem.TryGenerateStartingBalance(bankAccount, jobPrototype);

                if (AttachWage)
                {
                    _wageManagerSystem.TryAddAccountToWagePayoutList(bankAccount, jobPrototype);
                }
            }
        }

        if (_roleSystem.MindHasRole<BankMemoryComponent>(mindId))
        {
            _roleSystem.MindRemoveRole<BankMemoryComponent>(mindId);
        }

        _roleSystem.MindAddRole(mindId, new BankMemoryComponent
        {
            BankAccount = idCardUid,
            AccountNumber = bankAccount.AccountNumber,
            AccountPin = bankAccount.AccountPin
        });

        var needAdd = true;
        foreach (var condition in mind.AllObjectives.Where(HasComp<MindNoteConditionComponent>))
        {
            var md = Comp<MindNoteConditionComponent>(condition);
            Dirty(condition,md);
            needAdd = false;
        }

        if (needAdd)
        {
            _mindSystem.TryAddObjective(mindId, mind, BankNoteCondition);
        }

        return (idCardUid, bankAccount);
    }
    #endregion

    [ValidatePrototypeId<EntityPrototype>] private const string BankNoteCondition = "BankNote";
}
