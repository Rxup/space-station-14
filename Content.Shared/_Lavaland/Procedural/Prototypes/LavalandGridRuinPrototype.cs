﻿using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Lavaland.Procedural.Prototypes;

/// <summary>
/// Contains information about Lavaland ruin configuration.
/// </summary>
[Prototype]
public sealed partial class LavalandGridRuinPrototype : IPrototype
{
    [IdDataField] public string ID { get; } = default!;

    [DataField] public LocId Name = "lavaland-ruin-unknown";

    [DataField(required: true)]
    public ResPath Path { get; } = default!;

    [DataField]
    public int SpawnAttemps = 16;

    [DataField(required: true)]
    public int Priority = int.MinValue;

    /// <summary>
    /// List of components to grant to entities that enter the ruin.
    /// </summary>
    [DataField]
    public ComponentRegistry ComponentsToGrant = new();
}
