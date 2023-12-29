namespace Content.Server.Backmen.Species.Shadowkin.Components;

[RegisterComponent]
public sealed partial class ShadowkinRestPowerComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public bool IsResting = false;

    public EntityUid? ShadowkinRestAction;
}
