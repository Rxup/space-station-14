using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Psionics.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PotentialPsionicComponent : Component
{
    [DataField("chance"), ViewVariables(VVAccess.ReadWrite)]
    public float Chance = 0.04f;

    /// <summary>
    /// YORO (you only reroll once)
    /// </summary>
    [AutoNetworkedField]
    public bool Rerolled = false;
}
