using Content.Corvax.Interfaces.Shared;

namespace Content.Corvax.Interfaces.Client;

public interface IClientSponsorsManager : ISharedSponsorsManager
{
    public List<string> Prototypes { get; }
    public bool PriorityJoin { get; }
    public Color? OocColor { get; }
    public int ExtraCharSlots { get; }
    public string? GhostTheme { get; }
}
