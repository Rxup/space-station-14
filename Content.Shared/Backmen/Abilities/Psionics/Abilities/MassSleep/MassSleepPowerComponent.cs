using Content.Shared.Actions.ActionTypes;

namespace Content.Shared.Backmen.Abilities.Psionics;

[RegisterComponent]
public sealed partial class MassSleepPowerComponent : Component
{
    public WorldTargetAction? MassSleepPowerAction = null;

    public float Radius = 1.25f;
}
