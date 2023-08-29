//using Content.Server.Backmen.Economy.ATM;

using System.Linq;
using Content.Server.Access.Systems;
using Content.Server.Backmen.CartridgeLoader.Cartridges;
using Content.Server.Backmen.Economy.ATM;
using Content.Server.Backmen.Economy.Eftpos;
using Content.Server.Backmen.Economy.Wage;
using Content.Server.Backmen.Mind;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Mind;
using Content.Server.Mind.Components;
using Content.Server.Objectives;
using Content.Server.Roles;
using Content.Server.Roles.Jobs;
using Content.Server.Store.Components;
using Content.Server.Store.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Backmen.Store;
using Content.Shared.CartridgeLoader;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Store;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

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

    }

    private void OnFtposInit(EntityUid uid, EftposComponent component, ComponentInit args)
    {
        uid.EnsureComponentWarn<ServerUserInterfaceComponent>();
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
            var dummy = Spawn("CaptainIDCard",MapCoordinates.Nullspace);
            _metaDataSystem.SetEntityName(dummy,"Bank: "+department.AccountNumber);
            var bankAccount = _bankManagerSystem.CreateNewBankAccount(dummy, department.AccountNumber, true);
            if (bankAccount == null)
                continue;
            bankAccount.AccountName = department.ID;
            bankAccount.Balance = 100000;
        }
    }

    #endregion

    #region PublicApi
    [PublicAPI]
    public bool TryStoreNewBankAccount(EntityUid player, EntityUid uid, IdCardComponent? id, out BankAccountComponent? bankAccount)
    {
        bankAccount = null;
        if (!Resolve(uid, ref id))
            return false;
        bankAccount = _bankManager.CreateNewBankAccount(uid);
        if (bankAccount == null)
            return false;
        id.StoredBankAccountNumber = bankAccount.AccountNumber;
        id.StoredBankAccountPin = bankAccount.AccountPin;
        bankAccount.AccountName = id.FullName;
        if (string.IsNullOrEmpty(bankAccount.AccountName))
        {
            bankAccount.AccountName = MetaData(player).EntityName;
        }
        Dirty(id);
        return true;
    }
    [PublicAPI]
    public BankAccountComponent? AddPlayerBank(EntityUid Player, BankAccountComponent? bankAccount = null, bool AttachWage = true)
    {
        if (!_cardSystem.TryFindIdCard(Player, out var idCardComponent))
            return null;

        if (!_mindSystem.TryGetMind(Player, out var mindId, out var mind))
        {
            return null;
        }

        if (bankAccount == null)
        {
            if (!TryStoreNewBankAccount(Player,idCardComponent.Owner, idCardComponent, out bankAccount) || bankAccount == null)
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


        if (!_inventorySystem.TryGetSlotEntity(Player, "id", out var idUid))
            return bankAccount;

        if (!EntityManager.TryGetComponent(idUid, out CartridgeLoaderComponent? cartrdigeLoaderComponent))
            return bankAccount;

        foreach (var uid in cartrdigeLoaderComponent.InstalledPrograms)
        {
            if (!EntityManager.TryGetComponent(uid, out BankCartridgeComponent? bankCartrdigeComponent))
                continue;

            if (bankCartrdigeComponent.LinkedBankAccount == null)
            {
                _bankCartridgeSystem.LinkBankAccountToCartridge(bankCartrdigeComponent, bankAccount);
            }
            else if(bankCartrdigeComponent.LinkedBankAccount.AccountNumber != bankAccount.AccountNumber)
            {
                _bankCartridgeSystem.UnlinkBankAccountFromCartridge(bankCartrdigeComponent, bankCartrdigeComponent.LinkedBankAccount);
                _bankCartridgeSystem.LinkBankAccountToCartridge(bankCartrdigeComponent, bankAccount);
            }
            // else: do nothing
        }

        var objectives = mind.AllObjectives.ToList();
        foreach (var condition in objectives.Where(t => t.Prototype.ID == "BankNote").SelectMany(t => t.Conditions))
        {
            if (condition is not MindNoteCondition md)
            {
                continue;
            }

            md.Owner = bankAccount;
        }

        _mindSystem.TryAddObjective(mindId, mind, _prototype.Index<ObjectivePrototype>("BankNote"));

        return bankAccount;
    }
    #endregion
}
