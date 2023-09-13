using Content.Shared.Actions.Events;

namespace Content.Shared.Backmen.Abilities.Psionics;

[RegisterComponent]
public sealed partial class MassSleepPowerComponent : Component
{
    public EntityUid? MassSleepPowerAction = null;

    public float Radius = 1.25f;
}
