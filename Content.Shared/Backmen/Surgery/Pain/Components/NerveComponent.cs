using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery.Pain.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NerveComponent : Component
{
    //Yuh-uh
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 PainMultiplier = 1.0f;

    /// <summary>
    /// Nerve system, to which this nerve is parented.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public EntityUid ParentedNerveSystem;
}
