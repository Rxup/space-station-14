﻿using Robust.Shared.GameStates;

namespace Content.Shared.Chemistry.Components;

[NetworkedComponent]
public abstract partial class SharedSmokeComponent : Component
{
    [DataField("color")]
    public Color Color = Color.White;
}
