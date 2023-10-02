using Robust.Shared.GameStates;

namespace Content.Shared.Chemistry.Components;

[NetworkedComponent, AutoGenerateComponentState]
public abstract partial class SharedSmokeComponent : Component
{
    [DataField("color"), AutoNetworkedField]
    public Color Color = Color.White;
}
