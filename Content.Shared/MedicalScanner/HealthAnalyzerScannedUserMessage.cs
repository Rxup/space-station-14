using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Targeting;
using Robust.Shared.Serialization;

namespace Content.Shared.MedicalScanner;

/// <summary>
///     On interacting with an entity retrieves the entity UID for use with getting the current damage of the mob.
/// </summary>
[Serializable, NetSerializable]
public sealed class HealthAnalyzerScannedUserMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity? TargetEntity;
    public float Temperature;
    public float BloodLevel;
    public bool? ScanMode;
    public bool? Bleeding;
    public bool? Unrevivable;
    public Dictionary<TargetBodyPart, WoundableSeverity>? Body; // backmen: surgery
    public NetEntity? Part; // backmen: surgery

    public HealthAnalyzerScannedUserMessage(NetEntity? targetEntity, float temperature, float bloodLevel, bool? scanMode, bool? bleeding, bool? unrevivable, Dictionary<TargetBodyPart, WoundableSeverity>? body, NetEntity? part = null)
    {
        TargetEntity = targetEntity;
        Temperature = temperature;
        BloodLevel = bloodLevel;
        ScanMode = scanMode;
        Bleeding = bleeding;
        Unrevivable = unrevivable;
        Body = body; // backmen: surgery
        Part = part; // backmen: surgery
    }
}

[Serializable, NetSerializable]
public sealed class HealthAnalyzerPartMessage(NetEntity? owner, TargetBodyPart? bodyPart) : BoundUserInterfaceMessage
{
    public readonly NetEntity? Owner = owner;
    public readonly TargetBodyPart? BodyPart = bodyPart;

}

