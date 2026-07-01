// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;

namespace Content.Shared._Lua.Shuttles.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ShuttleGrapplingHookProjectileComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? Weapon;

    [ViewVariables(VVAccess.ReadWrite)]
    public string? JointId;

    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? OwnerGrid;

    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? TargetGrid;
}

