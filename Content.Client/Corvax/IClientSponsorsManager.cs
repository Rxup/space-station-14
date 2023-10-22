using Content.Corvax.Interfaces.Shared;
using Robust.Shared.Maths;

namespace Content.Corvax.Interfaces.Client;

public interface IClientSponsorsManager : ISharedSponsorsManager
{
    public List<string> Prototypes { get; }
}
