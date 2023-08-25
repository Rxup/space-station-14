using Content.Server.Access.Systems;
using Content.Server.Backmen.CartridgeLoader.Cartridges;
using Content.Server.Backmen.Economy;
using Content.Server.Backmen.Economy.Wage;
using Content.Server.Objectives.Interfaces;
using Content.Shared.Inventory;
using JetBrains.Annotations;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Mind;

[UsedImplicitly]
[DataDefinition]
public sealed partial class MindNoteCondition : IObjectiveCondition
{

    // ReSharper disable once InconsistentNaming
    private static IEntityManager _entityManager => IoCManager.Resolve<IEntityManager>();
    // ReSharper disable once InconsistentNaming
    private static BankManagerSystem _BankManagerSystem => _entityManager.System<BankManagerSystem>();
    // ReSharper disable once InconsistentNaming
    private static IdCardSystem _cardSystem => _entityManager.System<IdCardSystem>();

    public BankAccountComponent? Owner { get; set; }

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

    private MindNoteCondition NewEmpty => new MindNoteCondition { Title = Loc.GetString("character-info-memories-placeholder-text")};

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

        var bank = _BankManagerSystem.GetBankAccount(idCardComponent.StoredBankAccountNumber);

        if (bank == null)
        {
            return NewEmpty;
        }
        return new MindNoteCondition()
        {
            Title =  Loc.GetString("character-info-memories-placeholder-text"),
            Owner = bank,
        };
    }

    public string Title { get; set; } = "";
    public string Description => Owner != null
        ? Loc.GetString("memory-account-number", ("value", Owner!.AccountNumber)) + "\n" +
          Loc.GetString("memory-account-pin", ("value", Owner!.AccountPin))
        : "";
    public SpriteSpecifier Icon { get; } = new SpriteSpecifier.Rsi(new ResPath("Backmen/Objects/Tools/rimbank.rsi"), "icon");
    public float Progress { get; } = 0;
    public float Difficulty { get; } = 0;
}
