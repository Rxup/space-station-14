using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.GhostTheme;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class GhostThemeComponent: Component
{
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField, DataField("ghostTheme")]
    public ProtoId<GhostThemePrototype>? GhostTheme;
}

[Serializable, NetSerializable]
public sealed class RequestGhostThemeEvent : EntityEventArgs
{
    public ProtoId<GhostThemePrototype> Ghost { get; }

    public RequestGhostThemeEvent(string ghostThemeId)
    {
        Ghost = ghostThemeId;
    }
}
