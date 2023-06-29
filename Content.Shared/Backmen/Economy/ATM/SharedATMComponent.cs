using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Economy.ATM;

[NetworkedComponent()]
public abstract class SharedATMComponent : Component
{
    public static string IdCardSlotId = "IdCardSlot";

    [DataField("idCardSlot")]
    public ItemSlot IdCardSlot = new();

    [Serializable, NetSerializable]
    public sealed class ATMBoundUserInterfaceState : BoundUserInterfaceState
    {
        public readonly bool IsCardPresent;
        public readonly string? IdCardFullName;
        public readonly string? IdCardEntityName;
        public readonly string? IdCardStoredBankAccountNumber;
        public readonly bool HaveAccessToBankAccount;
        public readonly FixedPoint2? BankAccountBalance;
        public readonly string? CurrencySymbol;
        public ATMBoundUserInterfaceState(
            bool isCardPresent,
            string? idCardFullName,
            string? idCardEntityName,
            string? idCardStoredBankAccountNumber,
            bool haveAccessToBankAccount,
            FixedPoint2? bankAccountBalance,
            string? currencySymbol)
        {
            IsCardPresent = isCardPresent;
            IdCardFullName = idCardFullName;
            IdCardEntityName = idCardEntityName;
            IdCardStoredBankAccountNumber = idCardStoredBankAccountNumber;
            HaveAccessToBankAccount = haveAccessToBankAccount;
            BankAccountBalance = bankAccountBalance;
            CurrencySymbol = currencySymbol;
        }
    }
}

[Serializable, NetSerializable]
public enum ATMVisuals
{
    VisualState
}

[Serializable, NetSerializable]
public enum ATMVisualState
{
    Normal,
    Off
}
