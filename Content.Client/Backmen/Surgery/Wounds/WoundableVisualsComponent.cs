using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.FixedPoint;

namespace Content.Client.Backmen.Surgery.Wounds;

[RegisterComponent]
public sealed partial class WoundableVisualsComponent : Component
{
    [DataField] public Dictionary<string, WoundVisualizerSprite>? DamageOverlayGroups = new();

    [DataField] public string BleedingOverlay;

    [DataField] public List<Enum>? TargetLayers;

    [DataField(required: true)]
    public List<FixedPoint2> Thresholds = [];

    [DataField] public Dictionary<BleedingSeverity, FixedPoint2> BleedingThresholds = new()
    {
        { BleedingSeverity.Minor, 0.05 },
        { BleedingSeverity.Severe, 0.30 },
    };

    public HashSet<Enum> TargetLayerMapKeys = [];
    public HashSet<EntityUid> DroppedBodyParts = [];
}

// :fort:
[DataDefinition]
public sealed partial class WoundVisualizerSprite
{
    [DataField(required: true)] public string Sprite = default!;

    [DataField] public string? Color;
}
