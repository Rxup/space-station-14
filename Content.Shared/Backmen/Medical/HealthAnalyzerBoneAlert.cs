using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Backmen.Targeting;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Medical;

[Serializable, NetSerializable]
public struct HealthAnalyzerBoneAlert
{
    public TargetBodyPart BodyPart;
    public BoneSeverity Severity;
}
