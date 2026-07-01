// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

namespace Content.Shared._Lua.Shuttles.Components;

[RegisterComponent]
public sealed partial class MagneticGrabberComponent : Component
{
    [DataField]
    public bool Enabled = true;
}

