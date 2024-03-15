using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Vampiric;

public enum VampireStoreType
{
    None,
    Tier1Upgrade,
    Tier2Upgrade,
    Tier3Upgrade,
    MakeNewVamp,
    SkillMouse1,
    SkillMouse2,
    Sprint1,
    Sprint2,
    Sprint3,
    Sprint4,
    Sprint5,
    NoSlip,
    DispelPower,
    RegenPower,
    ZapPower,
    PsiInvisPower,
    IgnitePower
}

[Serializable, NetSerializable]
[ImplicitDataDefinitionForInheritors]
public sealed partial class VampireStoreEvent : EntityEventArgs
{
    [DataField("buyType")]
    public VampireStoreType BuyType;
}

public sealed partial class VampireShopActionEvent : InstantActionEvent
{

}
