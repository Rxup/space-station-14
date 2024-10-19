using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Disease;

/// This is added to anyone with at least 1 disease
/// and helps cull event subscriptions and entity queries
/// when they are not relevant.
[RegisterComponent]
[NetworkedComponent]
public sealed partial class DiseasedComponent : Component
{

}
