// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System.Numerics;

namespace Content.Shared._Lua.Shuttles.Components;

[RegisterComponent]
public sealed partial class MagneticLatchComponent : Component
{
    [DataField]
    public string? JointId;

    [DataField]
    public EntityUid? OwnerGrid;

    [DataField]
    public EntityUid? TargetGrid;

    [DataField]
    public EntityUid? LatchedToEntity;

    [DataField]
    public Vector2? LocalAnchorOwner;

    [DataField]
    public Vector2? LocalAnchorTarget;

    [DataField]
    public float? ReferenceAngle;
}

