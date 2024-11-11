namespace Content.Server.Backmen.Psionics.Objectives.Components;

[RegisterComponent, Access(typeof(RaiseGlimmerConditionSystem))]
public sealed partial class RaiseGlimmerConditionComponent : Component
{
    [DataField("target"), ViewVariables(VVAccess.ReadWrite)]
    public int Target;
}
