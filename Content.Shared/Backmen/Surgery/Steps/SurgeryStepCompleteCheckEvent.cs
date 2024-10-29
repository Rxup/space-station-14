namespace Content.Shared.Backmen.Surgery.Steps;

[ByRefEvent]
public record struct SurgeryStepCompleteCheckEvent(EntityUid Body, EntityUid Part, EntityUid Surgery, bool Cancelled = false);
