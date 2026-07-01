// LuaWorld - zone generator markers (components only; systems not ported)
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Lua.Zones;

[RegisterComponent]
public sealed partial class FrontierZoneGeneratorComponent : Component
{
    [DataField]
    public int Radius = 5;
}

[RegisterComponent]
public sealed partial class NoFlightZoneGeneratorComponent : Component
{
    [DataField]
    public int Radius = 5;
}

[RegisterComponent]
public sealed partial class ExpeditionZoneGeneratorComponent : Component
{
    [DataField]
    public int Radius = 5;
}

[RegisterComponent]
public sealed partial class NfsdZoneGeneratorComponent : Component
{
    [DataField]
    public int Radius = 5;
}

[RegisterComponent]
public sealed partial class PacifiedZoneGeneratorComponent : Component
{
    [DataField]
    public int Radius = 5;

    [DataField]
    public List<ProtoId<JobPrototype>> ImmuneRoles = new();
}
