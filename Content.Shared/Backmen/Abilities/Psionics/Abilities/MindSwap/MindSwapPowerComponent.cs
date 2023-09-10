using Content.Shared.Actions.ActionTypes;

namespace Content.Shared.Backmen.Abilities.Psionics;

[RegisterComponent]
public sealed partial class MindSwapPowerComponent : Component
{
    public EntityTargetAction? MindSwapPowerAction = null;
}
