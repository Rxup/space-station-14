using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Nutrition.Components;
using Robust.Shared.Serialization;

namespace Content.Shared.MedicalScanner;

/// <summary>
/// On interacting with an entity retrieves the entity UID for use with getting the current damage of the mob.
/// </summary>
[Serializable, NetSerializable]
public sealed class HealthAnalyzerScannedUserMessage : BoundUserInterfaceMessage
{
    public HealthAnalyzerUiState State;

    public NetEntity? TargetEntity => State.TargetEntity;
    public float Temperature => State.Temperature;
    public float BloodLevel => State.BloodLevel;
    public bool? ScanMode => State.ScanMode;
    public bool? Bleeding => State.Bleeding;
    public bool? Unrevivable => State.Unrevivable;
    public Dictionary<TargetBodyPart, WoundableSeverity>? Body => State.Body;
    public NetEntity? Part => State.Part;
    public Dictionary<string, float>? PainCauses => State.PainCauses;
    public float? TotalPain => State.TotalPain;
    public bool? PainImmune => State.PainImmune;
    // start-backmen: analyzer-satiation
    public HungerThreshold? HungerAlert => State.HungerAlert;
    public ThirstThreshold? ThirstAlert => State.ThirstAlert;
    // end-backmen: analyzer-satiation

    public HealthAnalyzerScannedUserMessage(HealthAnalyzerUiState state)
    {
        State = state;
    }
}

/// <summary>
/// Contains the current state of a health analyzer control. Used for the health analyzer and cryo pod.
/// </summary>
[Serializable, NetSerializable]
public struct HealthAnalyzerUiState
{
    public readonly NetEntity? TargetEntity;
    public float Temperature;
    public float BloodLevel;
    public bool? ScanMode;
    public bool? Bleeding;
    public bool? Unrevivable;
    public Dictionary<TargetBodyPart, WoundableSeverity>? Body; // backmen: surgery
    public NetEntity? Part; // backmen: surgery
    public Dictionary<string, float>? PainCauses; // backmen: pain
    public float? TotalPain; // backmen: pain
    public bool? PainImmune; // backmen: pain
    // start-backmen: analyzer-satiation
    public HungerThreshold? HungerAlert;
    public ThirstThreshold? ThirstAlert;
    // end-backmen: analyzer-satiation

    public HealthAnalyzerUiState(NetEntity? targetEntity, float temperature, float bloodLevel, bool? scanMode, bool? bleeding, bool? unrevivable, Dictionary<TargetBodyPart, WoundableSeverity>? body, NetEntity? part = null, Dictionary<string, float>? painCauses = null, float? totalPain = null, bool? painImmune = null, HungerThreshold? hungerAlert = null, ThirstThreshold? thirstAlert = null)
    {
        TargetEntity = targetEntity;
        Temperature = temperature;
        BloodLevel = bloodLevel;
        ScanMode = scanMode;
        Bleeding = bleeding;
        Unrevivable = unrevivable;
        Body = body; // backmen: surgery
        Part = part; // backmen: surgery
        PainCauses = painCauses; // backmen: pain
        TotalPain = totalPain; // backmen: pain
        PainImmune = painImmune; // backmen: pain
        // start-backmen: analyzer-satiation
        HungerAlert = hungerAlert;
        ThirstAlert = thirstAlert;
        // end-backmen: analyzer-satiation
    }
}

[Serializable, NetSerializable]
public sealed class HealthAnalyzerPartMessage(NetEntity? owner, TargetBodyPart? bodyPart) : BoundUserInterfaceMessage
{
    public readonly NetEntity? Owner = owner;
    public readonly TargetBodyPart? BodyPart = bodyPart;

}
