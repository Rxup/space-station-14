using Content.Shared.Actions;
using Content.Shared.Antag;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Vampiric;

[RegisterComponent]
[NetworkedComponent]
public sealed partial class BkmVampireComponent : Component, IAntagStatusIconComponent
{
    public ProtoId<StatusIconPrototype> StatusIcon { get; set; } = "VampireFaction";
    public bool IconVisibleToGhost { get; set; } = true;

    public EntityUid? ActionNewVamp;
    public ProtoId<EntityPrototype> NewVamp = "ActionConvertToVampier";
}

public sealed partial class InnateNewVampierActionEvent : EntityTargetActionEvent
{
}

[Serializable, NetSerializable]
public sealed partial class InnateNewVampierDoAfterEvent : SimpleDoAfterEvent
{
}
