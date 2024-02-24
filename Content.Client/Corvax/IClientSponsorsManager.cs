using Content.Corvax.Interfaces.Shared;
using Robust.Shared.Maths;

namespace Content.Corvax.Interfaces.Client;

public interface IClientSponsorsManager : ISharedSponsorsManager
{
    public HashSet<string> Prototypes { get; }
    public int Tier { get; }
    public bool Whitelisted { get; }
}
