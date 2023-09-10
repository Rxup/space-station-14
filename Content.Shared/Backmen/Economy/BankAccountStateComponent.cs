using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Economy;

[Serializable, NetSerializable]
public sealed class BankAccountStateComponent : ComponentState
{
    public FixedPoint2 Balance { get; set; }
}
