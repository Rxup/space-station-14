using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Surgery;

[Serializable, NetSerializable]
public enum SurgeryUIKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class SurgeryMissingPartChoice(
    NetEntity anchorPart,
    string label,
    List<EntProtoId> surgeries)
{
    public NetEntity AnchorPart = anchorPart;
    public string Label = label;
    public List<EntProtoId> Surgeries = surgeries;
}

[Serializable, NetSerializable]
public sealed class SurgeryBuiState(
    Dictionary<NetEntity, List<EntProtoId>> choices,
    List<SurgeryMissingPartChoice> missingParts) : BoundUserInterfaceState
{
    public readonly Dictionary<NetEntity, List<EntProtoId>> Choices = choices;
    public readonly List<SurgeryMissingPartChoice> MissingParts = missingParts;
}

[Serializable, NetSerializable]
public sealed class SurgeryBuiRefreshMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class SurgeryStepChosenBuiMsg(NetEntity part, EntProtoId surgery, EntProtoId step, bool isBody) : BoundUserInterfaceMessage
{
    public readonly NetEntity Part = part;
    public readonly EntProtoId Surgery = surgery;
    public readonly EntProtoId Step = step;

    // Used as a marker for whether or not we're hijacking surgery by applying it on the body itself.
    public readonly bool IsBody = isBody;
}
