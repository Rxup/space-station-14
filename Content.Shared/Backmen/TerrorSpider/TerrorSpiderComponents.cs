using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Antags.TerrorSpider;
[RegisterComponent]
public sealed partial class StealthOnWebComponent : Component
{
    [DataField]
    public int Collisions = 0;
}
[RegisterComponent]
public sealed partial class EggHolderComponent : Component
{
    [DataField]
    public int Counter = 0;
}
[RegisterComponent]
public sealed partial class HasEggHolderComponent : Component
{
}
[RegisterComponent]
public sealed partial class TerrorPrincessComponent : Component
{
}
public sealed partial class EggInjectionEvent : EntityTargetActionEvent
{
}
[Serializable, NetSerializable]
public sealed partial class EggInjectionDoAfterEvent : SimpleDoAfterEvent
{
}
[Serializable, NetSerializable]
public enum EggsLayingUiKey : byte
{
    Key
}

public sealed partial class EggsLayingEvent : InstantActionEvent
{

}
[Serializable, NetSerializable]
public sealed class EggsLayingBuiMsg : BoundUserInterfaceMessage
{
    public EntProtoId Egg { get; set; }
}
[Serializable, NetSerializable]
public sealed class EggsLayingBuiState : BoundUserInterfaceState
{
}
