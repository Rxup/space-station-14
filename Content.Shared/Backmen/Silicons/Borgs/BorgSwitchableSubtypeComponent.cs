using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Silicons.Borgs;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
[Access(typeof(SharedBorgSwitchableSubtypeSystem))]
public sealed partial class BorgSwitchableSubtypeComponent : Component
{
    /// <summary>
    /// The <see cref="BorgSubtypePrototype"/> of this chassis.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<BorgSubtypePrototype>? BorgSubtype;
}
