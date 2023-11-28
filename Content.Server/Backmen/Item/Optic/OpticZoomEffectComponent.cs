namespace Content.Server.Backmen.Item.Optic;

[RegisterComponent]
public sealed partial class OpticZoomEffectComponent : Component
{
    [DataField("targetZoom"), ViewVariables(VVAccess.ReadOnly)]
    public float TargetZoom { get; set; } = 2;
}
