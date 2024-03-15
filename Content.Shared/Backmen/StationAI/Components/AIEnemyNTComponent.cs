﻿using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.StationAI.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AIEnemyNTComponent : Component
{
    [AutoNetworkedField]
    public EntityUid? Source;
}
