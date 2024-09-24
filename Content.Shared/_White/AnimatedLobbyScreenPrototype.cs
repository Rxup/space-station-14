using Robust.Shared.Prototypes;

namespace Content.Shared._White;

[Prototype]
public sealed partial class AnimatedLobbyScreenPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    [DataField(required: true)]
    public string Path = default!;
}
