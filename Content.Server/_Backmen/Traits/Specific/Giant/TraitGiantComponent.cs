namespace Content.Server._Backmen.Traits.Specific.Giant;

[RegisterComponent]
public sealed partial class TraitGiantComponent : Component
{
    [DataField]
    public float Scale { get; set; }
}
