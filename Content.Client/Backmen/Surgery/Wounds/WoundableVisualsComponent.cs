﻿using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.FixedPoint;

namespace Content.Client.Backmen.Surgery.Wounds;

[RegisterComponent]
public sealed partial class WoundableVisualsComponent : Component
{
    [DataField(required: true)] public Enum OccupiedLayer;

    [DataField] public Dictionary<string, WoundVisualizerSprite>? DamageOverlayGroups = new();
    [DataField] public string? BleedingOverlay;

    [DataField(required: true)] public List<FixedPoint2> Thresholds = [];
    [DataField] public Dictionary<BleedingSeverity, FixedPoint2> BleedingThresholds = new()
    {
        { BleedingSeverity.Minor, 2.6 },
        { BleedingSeverity.Severe, 7 },
    };
}

// :fort:
[DataDefinition]
public sealed partial class WoundVisualizerSprite
{
    [DataField(required: true)] public string Sprite = default!;

    [DataField] public string? Color;
}
