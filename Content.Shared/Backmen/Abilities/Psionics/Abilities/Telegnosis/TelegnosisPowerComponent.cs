namespace Content.Shared.Backmen.Abilities.Psionics;

[RegisterComponent]
public sealed partial class TelegnosisPowerComponent : Component
{
    [DataField("prototype")]
    public string Prototype = "MobObserverTelegnostic";
    public EntityUid? TelegnosisPowerAction = null;
}
