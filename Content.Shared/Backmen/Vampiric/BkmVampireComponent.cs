using Content.Shared.Actions;
using Content.Shared.Antag;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.StatusIcon;
using Content.Shared.Store;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Backmen.Vampiric;

[RegisterComponent]
[NetworkedComponent]
public sealed partial class BkmVampireComponent : Component
{
    [DataField("currencyPrototype", customTypeSerializer: typeof(PrototypeIdSerializer<CurrencyPrototype>))]
    public string CurrencyPrototype = "BloodEssence";


    [ViewVariables(VVAccess.ReadWrite)]
    public int SprintLevel = 0;

    public EntityUid? ActionNewVamp;
    public EntProtoId NewVamp = "ActionConvertToVampier";

    public Dictionary<string, FixedPoint2> DNA = new();
}

public sealed partial class InnateNewVampierActionEvent : EntityTargetActionEvent
{
}

[Serializable, NetSerializable]
public sealed partial class InnateNewVampierDoAfterEvent : SimpleDoAfterEvent
{
}
