using Content.Server.Access.Systems;
using Content.Server.Backmen.CartridgeLoader.Cartridges;
using Content.Server.Backmen.Economy;
using Content.Server.Backmen.Economy.Wage;
using Content.Server.Objectives.Interfaces;
using Content.Shared.Inventory;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Mind;

public sealed class MindNoteCondition : IObjectiveCondition
{
    private IEntityManager _entityManager => IoCManager.Resolve<IEntityManager>();
    private BankManagerSystem _BankManagerSystem => _entityManager.System<BankManagerSystem>();
    /*
    private WageManagerSystem _wageManagerSystem => _entityManager.System<WageManagerSystem>();

    private InventorySystem _inventorySystem => _entityManager.System<InventorySystem>();*/
    private IdCardSystem _cardSystem => _entityManager.System<IdCardSystem>();

    private BankAccountComponent? Owner { get; set; }

    public bool Equals(IObjectiveCondition? other)
    {
        return other is MindNoteCondition kpc && Equals(Owner, kpc.Owner);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        return obj.GetType() == GetType() && Equals((MindNoteCondition) obj);
    }
    public override int GetHashCode()
    {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return Owner?.GetHashCode() ?? 0;
    }

    private MindNoteCondition NewEmpty => new MindNoteCondition
    {
        Title = Loc.GetString("character-info-memories-placeholder-text"),
        Description = ""
    };
    public IObjectiveCondition GetAssigned(Server.Mind.Mind mind)
    {
        var entity = mind.OwnedEntity;
        if (!entity.HasValue)
        {
            return NewEmpty;
        }
        if (!_cardSystem.TryFindIdCard(entity.Value, out var idCardComponent))
            return NewEmpty;

        if ( idCardComponent.StoredBankAccountNumber == null || idCardComponent.StoredBankAccountPin == null)
        {
            return NewEmpty;
        }

        var bankAccount = _BankManagerSystem.GetBankAccount(idCardComponent.StoredBankAccountNumber);

        if (bankAccount == null)
        {
            return NewEmpty;
        }

        return new MindNoteCondition()
        {
            Title = Loc.GetString("character-info-memories-placeholder-text"),
            Owner = bankAccount,
            Description = Loc.GetString("memory-account-number",("value",bankAccount.AccountNumber))+"\n"+Loc.GetString("memory-account-pin",("value",bankAccount.AccountPin))
        };
    }

    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public SpriteSpecifier Icon { get; } = SpriteSpecifier.Invalid;
    public float Progress { get; } = 0;
    public float Difficulty { get; } = 0;
}
