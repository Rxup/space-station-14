using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Psionics;

[RegisterComponent, NetworkedComponent]
public sealed partial class PsionicallyInvisibleComponent : Component
{
    public override bool SendOnlyToOwner => true;
}
