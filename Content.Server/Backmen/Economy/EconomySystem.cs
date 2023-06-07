//using Content.Server.Backmen.Economy.ATM;

using Content.Server.Access.Systems;
using Content.Server.Backmen.CartridgeLoader.Cartridges;
using Content.Server.Backmen.Economy.ATM;
using Content.Server.Backmen.Economy.Wage;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Mind.Components;
using Content.Server.Objectives;
using Content.Server.Store.Components;
using Content.Server.Store.Systems;
using Content.Shared.Access.Components;
using Content.Shared.CartridgeLoader;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Roles;
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
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned, after: new []{ typeof(Corvax.Loadout.LoadoutSystem) });
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStartingEvent);
    }

    private void OnRoundStartingEvent(RoundStartingEvent ev)
    {
        foreach (var department in _prototype.EnumeratePrototypes<DepartmentPrototype>())
        {
            var bankAccount = _bankManagerSystem.CreateNewBankAccount(department.AccountNumber, true);
            if (bankAccount == null) continue;
            bankAccount.AccountName = department.ID;
            bankAccount.Balance = 100000;
        }
    }

    public bool TryStoreNewBankAccount(EntityUid uid, IdCardComponent? id, out BankAccountComponent? bankAccount)
    {
        bankAccount = null;
        if (!Resolve(uid, ref id))
            return false;
        bankAccount = _bankManager.CreateNewBankAccount();
        if (bankAccount == null)
            return false;
        id.StoredBankAccountNumber = bankAccount.AccountNumber;
        id.StoredBankAccountPin = bankAccount.AccountPin;
        bankAccount.AccountName = id.FullName;
        Dirty(id);
        return true;
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (!_cardSystem.TryFindIdCard(ev.Mob, out var idCardComponent))
            return;

        if (!TryStoreNewBankAccount(idCardComponent.Owner, idCardComponent, out var bankAccount) ||
            bankAccount == null || !TryComp<MindComponent>(ev.Mob, out var mindComponent) || mindComponent.Mind == null || mindComponent.Mind.CurrentJob == null)
        {
            return;
        }
        var mind = mindComponent.Mind!;
        var jobPrototype = mind.CurrentJob!.Prototype;
        _bankManagerSystem.TryGenerateStartingBalance(bankAccount, jobPrototype);
        _wageManagerSystem.TryAddAccountToWagePayoutList(bankAccount, jobPrototype);
        if (!_inventorySystem.TryGetSlotEntity(ev.Mob, "id", out var idUid))
            return;

        if (!EntityManager.TryGetComponent(idUid, out CartridgeLoaderComponent? cartrdigeLoaderComponent))
            return;

        foreach (var uid in cartrdigeLoaderComponent.InstalledPrograms)
        {
            if (!EntityManager.TryGetComponent(uid, out BankCartridgeComponent? bankCartrdigeComponent))
                continue;

            _bankCartridgeSystem.LinkBankAccountToCartridge(bankCartrdigeComponent, bankAccount);
        }
        mind.TryAddObjective(_prototype.Index<ObjectivePrototype>("BankNote"));
    }
}
