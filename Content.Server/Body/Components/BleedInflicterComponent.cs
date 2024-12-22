using Content.Server.Body.Systems;

namespace Content.Server.Body.Components;

[RegisterComponent, Access(typeof(BloodstreamSystem))]
public sealed partial class BleedInflicterComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public bool IsBleeding;
}
