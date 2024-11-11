using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Changeling.Components;


/// <summary>
///     Component that indicates that a person's DNA has been absorbed by a changeling.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(AbsorbedSystem))]
public sealed partial class AbsorbedComponent : Component
{

}
