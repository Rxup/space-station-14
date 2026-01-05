using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Backmen.Lobby;

[Prototype]
public sealed partial class AnimatedLobbyScreenPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ResPath Path = default!;
}
