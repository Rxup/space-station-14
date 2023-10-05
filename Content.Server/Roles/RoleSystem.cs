//start-backmen: antag
using Content.Server.Backmen.EvilTwin;
using Content.Server.Backmen.Flesh;
using Content.Server.Backmen.Fugitive;
//end-backmen: antag
using Content.Shared.Roles;

namespace Content.Server.Roles;

public sealed class RoleSystem : SharedRoleSystem
{
    public override void Initialize()
    {
        // TODO make roles entities
        base.Initialize();

        SubscribeAntagEvents<DragonRoleComponent>();
        SubscribeAntagEvents<InitialInfectedRoleComponent>();
        SubscribeAntagEvents<NinjaRoleComponent>();
        SubscribeAntagEvents<NukeopsRoleComponent>();
        SubscribeAntagEvents<RevolutionaryRoleComponent>();
        SubscribeAntagEvents<SubvertedSiliconRoleComponent>();
        SubscribeAntagEvents<TraitorRoleComponent>();
        SubscribeAntagEvents<ZombieRoleComponent>();

        //start-backmen: antag
        SubscribeAntagEvents<BlobRoleComponent>();
        SubscribeAntagEvents<EvilTwinRoleComponent>();
        SubscribeAntagEvents<FugitiveRoleComponent>();
        SubscribeAntagEvents<FleshCultistRoleComponent>();
        //end-backmen: antag
    }

    public string? MindGetBriefing(EntityUid? mindId)
    {
        if (mindId == null)
            return null;

        var ev = new GetBriefingEvent();
        RaiseLocalEvent(mindId.Value, ref ev);
        return ev.Briefing;
    }
}

/// <summary>
/// Event raised on the mind to get its briefing.
/// Handlers can either replace or append to the briefing, whichever is more appropriate.
/// </summary>
[ByRefEvent]
public record struct GetBriefingEvent(string? Briefing = null);
