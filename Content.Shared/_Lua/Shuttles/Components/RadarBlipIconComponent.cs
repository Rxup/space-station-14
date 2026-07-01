// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld/LuaCorp Contributors
// See AGPLv3.txt for details.
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._Lua.Shuttles.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RadarBlipIconComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ResPath Icon;

    [DataField, AutoNetworkedField]
    public float Scale = 1f;

    [DataField, AutoNetworkedField]
    public float MaxDistance = 0f;

    [DataField, AutoNetworkedField]
    public bool RequireDetection = false;

    [DataField, AutoNetworkedField]
    public bool AllowWhenHidden = true;
}


