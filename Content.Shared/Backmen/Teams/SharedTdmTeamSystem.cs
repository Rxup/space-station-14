using Content.Shared.Administration;
using Content.Shared.Administration.Managers;
using Content.Shared.Backmen.Teams.Components;
using Content.Shared.Database;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Backmen.Teams;

public abstract class SharedTdmTeamSystem : EntitySystem
{
    [Dependency] private readonly NpcFactionSystem _npcFactionSystem = default!;
    [Dependency] private readonly ISharedAdminManager _adminManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TdmMemberComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(GetVerbs);

        TdmMemberComponentQuery = GetEntityQuery<TdmMemberComponent>();
    }

    protected EntityQuery<TdmMemberComponent> TdmMemberComponentQuery { get; set; }

    private void GetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!_adminManager.HasAdminFlag(args.User, AdminFlags.Fun))
            return;

        var target = args.Target;

        StationTeamMarker? team = null;

        if (TdmMemberComponentQuery.TryGetComponent(args.Target, out var targetTeam))
        {
            team = targetTeam.Team;
        }

        if(team != StationTeamMarker.TeamA)
        {
            Verb verb = new();
            verb.Text = Loc.GetString("prayer-verbs-team-a");
            verb.Message = "Назначить команду красных";
            verb.Category = VerbCategory.Tricks;
            verb.Icon = new SpriteSpecifier.Texture(new("/Textures/Backmen/Interface/Misc/svs_icon.rsi/team_a.png"));
            verb.Act = () =>
            {
                SetTeam(target, StationTeamMarker.TeamA);
            };
            verb.Impact = LogImpact.High;
            args.Verbs.Add(verb);
        }

        if(team != StationTeamMarker.TeamB)
        {
            Verb verb = new();
            verb.Text = Loc.GetString("prayer-verbs-team-b");
            verb.Message = "Назначить команду синих";
            verb.Category = VerbCategory.Tricks;
            verb.Icon = new SpriteSpecifier.Texture(new("/Textures/Backmen/Interface/Misc/svs_icon.rsi/team_b.png"));
            verb.Act = () =>
            {
                SetTeam(target, StationTeamMarker.TeamB);
            };
            verb.Impact = LogImpact.High;
            args.Verbs.Add(verb);
        }

        if(team != StationTeamMarker.Neutral)
        {
            Verb verb = new();
            verb.Text = Loc.GetString("prayer-verbs-team-0");
            verb.Message = "Назначить команду нетральных";
            verb.Category = VerbCategory.Tricks;
            verb.Icon = new SpriteSpecifier.Texture(new("/Textures/Backmen/Interface/Misc/svs_icon.rsi/team_0.png"));
            verb.Act = () =>
            {
                SetTeam(target, StationTeamMarker.Neutral);
            };
            verb.Impact = LogImpact.High;
            args.Verbs.Add(verb);
        }
    }

    protected abstract void SetTeam(Entity<TdmMemberComponent?> target, StationTeamMarker team);


    [ValidatePrototypeId<NpcFactionPrototype>]
    private const string TeamAFaction = "TeamA";
    [ValidatePrototypeId<NpcFactionPrototype>]
    private const string TeamBFaction = "TeamB";
    [ValidatePrototypeId<NpcFactionPrototype>]
    private const string TeamNeutralFaction = "Team0";

    private void OnMapInit(Entity<TdmMemberComponent> ent, ref MapInitEvent args)
    {
        SetFaction(ent!, ent.Comp.Team);
    }

    public void SetFaction(Entity<TdmMemberComponent?> ent, StationTeamMarker team)
    {
        if(!Resolve(ent, ref ent.Comp))
            return;

        EnsureComp<StatusIconComponent>(ent);
        ent.Comp.Team = team;
        Dirty(ent);

        Entity<NpcFactionMemberComponent?> factionEnt = (ent.Owner, EnsureComp<NpcFactionMemberComponent>(ent));
        _npcFactionSystem.ClearFactions(factionEnt, false);
        var faction = ent.Comp.Team switch
        {
            StationTeamMarker.TeamA => TeamAFaction,
            StationTeamMarker.TeamB => TeamBFaction,
            _ => TeamNeutralFaction,
        };
        _npcFactionSystem.AddFaction(factionEnt, faction);
    }
}
