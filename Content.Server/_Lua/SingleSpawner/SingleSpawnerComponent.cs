// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Prototypes;

namespace Content.Server._Lua.SingleSpawner;

[RegisterComponent]
public sealed partial class SingleSpawnerComponent : Component
{
    [DataField]
    public List<EntProtoId> CommonPrototypes = new();

    [DataField]
    public List<EntProtoId> RarePrototypes = new();

    [DataField]
    public List<EntProtoId> SuperRarePrototypes = new();

    [DataField]
    public float CommonChance = 1.0f;

    [DataField]
    public float RareChance = 0.1f;

    [DataField]
    public float SuperRareChance = 0.01f;

    public EntityUid? SpawnedEntity;
}
