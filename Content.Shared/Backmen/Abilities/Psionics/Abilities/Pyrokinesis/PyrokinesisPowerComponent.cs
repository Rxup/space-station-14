namespace Content.Shared.Backmen.Abilities.Psionics;

[RegisterComponent]
public sealed partial class PyrokinesisPowerComponent : Component
{
    [DataField]
    public int FireStacks = 5;

    public EntityUid? PyrokinesisPowerAction = null;
}
