using Content.Shared.Actions.ActionTypes;

namespace Content.Shared.Backmen.Abilities.Psionics;

[RegisterComponent]
public sealed partial class PyrokinesisPowerComponent : Component
{
    public EntityTargetAction? PyrokinesisPowerAction = null;
}
