using Content.Shared.Backmen.GhostTheme;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Backmen.GhostTheme;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]

public sealed partial class GhostThemeComponent: Component
{
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField,
     DataField("ghostTheme")]
    public string? GhostTheme;
}
