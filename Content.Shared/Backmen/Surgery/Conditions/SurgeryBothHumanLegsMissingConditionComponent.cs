using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery.Conditions;

/// <summary>
/// Requires that both human leg categories are absent from the patient.
/// Used for spider cephalothorax graft after bilateral leg amputation.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryBothHumanLegsMissingConditionComponent : Component;
