using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;

namespace Content.Client.Backmen.Surgery.Wounds;

[RegisterComponent]
public sealed partial class WoundableVisualsComponent : Component
{
    [DataField(customTypeSerializer: typeof(PrototypeIdDictionarySerializer<WoundVisualizerSprite, DamageGroupPrototype>))]
    public Dictionary<string, WoundVisualizerSprite>? DamageOverlayGroups = new();

    [DataField(required: true)] public Enum OccupiedLayer;
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
