using Content.Client.Players.PlayTimeTracking;
using Content.Corvax.Interfaces.Client;
using Content.Shared.Roles;
using Robust.Shared.Utility;

// ReSharper disable once CheckNamespace
namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private IClientDiscordAuthManager _discordAuthMgr = default!;
    private JobRequirementsManager _jobRequirementsMgr = default!;

    private void InitializeBkm()
    {
        _discordAuthMgr = IoCManager.Resolve<IClientDiscordAuthManager>();
        _jobRequirementsMgr = IoCManager.Resolve<JobRequirementsManager>();
    }

    private void BkmCheckReq(AntagPrototype antag, ref bool unlocked, ref FormattedMessage? reason)
    {
        if (antag.DiscordRequired && _discordAuthMgr.IsEnabled && !_discordAuthMgr.IsVerified)
        {
            unlocked = false;
            reason = FormattedMessage.FromUnformatted(Loc.GetString("role-required-discord"));
            return;
        }

        if (_jobRequirementsMgr.RoleBans.Contains("Antag:" + antag.ID))
        {
            unlocked = false;
            reason = FormattedMessage.FromUnformatted(Loc.GetString("role-ban"));
            return;
        }
    }
}
