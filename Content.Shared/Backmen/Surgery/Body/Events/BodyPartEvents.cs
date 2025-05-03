using Content.Shared.Body.Components;
using Content.Shared.Body.Part;

namespace Content.Shared.Backmen.Surgery.Body.Events;

[ByRefEvent]
public readonly record struct BodyPartEnableChangedEvent(bool Enabled);

[ByRefEvent]
public readonly record struct BodyPartEnabledEvent(Entity<BodyPartComponent> Part);

[ByRefEvent]
public readonly record struct BodyPartDisabledEvent(Entity<BodyPartComponent> Part);

[ByRefEvent]
public readonly record struct BodyPartAddedEvent(string Slot, Entity<BodyPartComponent> Part);

[ByRefEvent]
public readonly record struct BodyPartRemovedEvent(string Slot, Entity<BodyPartComponent> Part);

public readonly record struct BodyPartComponentsModifyEvent(EntityUid Body, bool Add);
