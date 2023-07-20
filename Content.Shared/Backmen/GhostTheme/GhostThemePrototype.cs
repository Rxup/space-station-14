using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.GhostTheme;

[Prototype("ghostTheme")]
public sealed class GhostThemePrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; } = default!;

    [DataField("components")]
    [AlwaysPushInheritance]
    public ComponentRegistry Components { get; } = new();
}
