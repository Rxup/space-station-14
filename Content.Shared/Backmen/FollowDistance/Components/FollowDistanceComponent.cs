﻿using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.FollowDistance.Components;

/// <summary>
/// Component to set new values for <see cref="CameraFollow"/>
/// </summary>
[RegisterComponent]
public sealed partial class FollowDistanceComponent : Component
{
    // Probably don't touch this field in your prototype, this can cause unpredictable behavior
    // But you can try...
    [DataField("maxDistance"), ViewVariables(VVAccess.ReadWrite)]
    public Vector2 MaxDistance = new(0.0001f, 0.0001f);

    // Change this field in your prototype to set max distance, the lower the value, the more the player will be able to see.
    // DON'T SET THIS TO 0, it will cause max distance wouldn't work and player will be able to look all over the map.
    //  If you want to set a value lower than 1, use float values, but never 0 and less.
    [DataField("backStrength"), ViewVariables(VVAccess.ReadWrite)]
    public float BackStrength = 4f;

    // This fields for default player's max distance and back strength
    // This will set automatically on player pickup entity with FollowDistanceComponent
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public Vector2 DefaultMaxDistance = new Vector2(0.0001f, 0.0001f);

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float DefaultBackStrength = 4f;
}
