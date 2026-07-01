// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Audio;

namespace Content.Shared._Lua.Shuttles.Components;

[RegisterComponent]
public sealed partial class ShuttleTabletComponent : Component
{
    [ViewVariables]
    public EntityUid? LinkedConsole;

    [DataField]
    public float LinkRange = 300f; // Meters

    [DataField]
    public bool IgnoreSector = false;

    [DataField]
    public bool CombatTablet = false;

    [DataField]
    public SoundSpecifier LinkSound = new SoundPathSpecifier("/Audio/Machines/quickbeep.ogg");
}
