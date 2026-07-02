namespace Content.Server.Backmen.Procedural;

[Flags]
public enum RoomSetupRequirements : byte
{
    None = 0,
    Canisters = 1 << 0,
    Supermatter = 1 << 1,
}
