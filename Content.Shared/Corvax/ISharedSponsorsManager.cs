using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Network;

namespace Content.Corvax.Interfaces.Shared;

public interface ISharedSponsorsManager
{
    public void Initialize();

    // Client
    public virtual List<string> GetClientPrototypes()
    {
        return new List<string>();
    }

    // Server
    public virtual bool TryGetServerPrototypes(NetUserId userId, [NotNullWhen(true)] out List<string>? prototypes)
    {
        throw new NotImplementedException();
    }

    public virtual bool TryGetServerOocColor(NetUserId userId, [NotNullWhen(true)] out Color? color)
    {
        throw new NotImplementedException();
    }

    public virtual int GetServerExtraCharSlots(NetUserId userId)
    {
        throw new NotImplementedException();
    }

    public virtual bool HaveServerPriorityJoin(NetUserId userId)
    {
        throw new NotImplementedException();
    }

    // backmen
    public void Cleanup();

    public virtual bool TryGetGhostTheme(NetUserId userId, [NotNullWhen(true)] out string? ghostTheme)
    {
        throw new NotImplementedException();
    }

    public bool TryGetLoadouts(NetUserId userId, [NotNullWhen(true)] out List<string>? prototypes)
    {
        throw new NotImplementedException();
    }

    public List<string> GetClientLoadouts()
    {
        throw new NotImplementedException();
    }

    public bool IsClientAllRoles()
    {
        throw new NotImplementedException();
    }

    public bool IsServerAllRoles(NetUserId userId)
    {
        throw new NotImplementedException();
    }
}
