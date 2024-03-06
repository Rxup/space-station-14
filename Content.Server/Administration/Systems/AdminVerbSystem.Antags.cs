using Content.Server.GameTicking.Rules;
using Content.Server.Zombies;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.Mind.Components;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Administration.Systems;

public sealed partial class AdminVerbSystem
{
    [Dependency] private readonly ZombieSystem _zombie = default!;
    [Dependency] private readonly ThiefRuleSystem _thief = default!;
    [Dependency] private readonly TraitorRuleSystem _traitorRule = default!;
    [Dependency] private readonly NukeopsRuleSystem _nukeopsRule = default!;
    [Dependency] private readonly PiratesRuleSystem _piratesRule = default!;
    [Dependency] private readonly RevolutionaryRuleSystem _revolutionaryRule = default!;

    // All antag verbs have names so invokeverb works.
    private void AddAntagVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        var player = actor.PlayerSession;

        if (!_adminManager.HasAdminFlag(player, AdminFlags.Fun))
            return;

        if (!HasComp<MindContainerComponent>(args.Target))
            return;

        Verb traitor = new()
        {
            Text = Loc.GetString("admin-verb-text-make-traitor"),
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Structures/Wallmounts/posters.rsi"),
                "poster5_contraband"),
            Act = () =>
            {
                // if its a monkey or mouse or something dont give uplink or objectives
                var isHuman = HasComp<HumanoidAppearanceComponent>(args.Target);
                _traitorRule.MakeTraitorAdmin(args.Target, giveUplink: isHuman, giveObjectives: isHuman);
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-make-traitor"),
        };
        args.Verbs.Add(traitor);

        Verb blobAntag = new()
        {
            Text = Loc.GetString("admin-verb-text-make-blob"),
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new("/Textures/Backmen/Interface/Actions/blob.rsi"), "blobFactory"),
            Act = () =>
            {
                EnsureComp<Shared.Backmen.Blob.Components.BlobCarrierComponent>(args.Target).HasMind = HasComp<ActorComponent>(args.Target);
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-text-make-blob"),
        };
        args.Verbs.Add(blobAntag);

        Verb fleshLeaderCultist = new()
        {
            Text = Loc.GetString("admin-verb-text-make-flesh-leader-cultist"),
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new("/Textures/Structures/flesh_heart.rsi"), "base_heart"),
            Act = () =>
            {
                if (!TryComp<ActorComponent>(args.Target, out var actor))
                    return;

                EntityManager.System<Content.Server.Backmen.GameTicking.Rules.FleshCultRuleSystem>()
                    .MakeCultist(actor.PlayerSession);
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-text-make-flesh-leader-cultist"),
        };
        args.Verbs.Add(fleshLeaderCultist);

        Verb fleshCultist = new()
        {
            Text = Loc.GetString("admin-verb-text-make-flesh-cultist"),
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new("/Textures/Mobs/Aliens/FleshCult/flesh_cult_mobs.rsi"), "worm"),
            Act = () =>
            {
                if (!TryComp<ActorComponent>(args.Target, out var actor))
                    return;

                EntityManager.System<Content.Server.Backmen.GameTicking.Rules.FleshCultRuleSystem>()
                    .MakeCultist(actor.PlayerSession);
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-text-make-flesh-cultist"),
        };
        args.Verbs.Add(fleshCultist);

        Verb EvilTwin = new()
        {
            Text = "Make EvilTwin",
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi((new ResPath("/Textures/Structures/Wallmounts/posters.rsi")),
                "poster3_legit"),
            Act = () =>
            {
                EntityManager.System<Content.Server.Backmen.EvilTwin.EvilTwinSystem>()
                    .MakeTwin(out _, args.Target);
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-make-eviltwin"),
        };
        args.Verbs.Add(EvilTwin);

        Verb zombie = new()
        {
            Text = Loc.GetString("admin-verb-text-make-zombie"),
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/Actions/zombie-turn.png")),
            Act = () =>
            {
                _zombie.ZombifyEntity(args.Target);
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-make-zombie"),
        };
        args.Verbs.Add(zombie);


        Verb nukeOp = new()
        {
            Text = Loc.GetString("admin-verb-text-make-nuclear-operative"),
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new("/Textures/Structures/Wallmounts/signs.rsi"), "radiation"),
            Act = () =>
            {
                _nukeopsRule.MakeLoneNukie(args.Target);
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-make-nuclear-operative"),
        };
        args.Verbs.Add(nukeOp);

        Verb pirate = new()
        {
            Text = Loc.GetString("admin-verb-text-make-pirate"),
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new("/Textures/Clothing/Head/Hats/pirate.rsi"), "icon"),
            Act = () =>
            {
                _piratesRule.MakePirate(args.Target);
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-make-pirate"),
        };
        args.Verbs.Add(pirate);

        //todo come here at some point dear lort.
        Verb headRev = new()
        {
            Text = Loc.GetString("admin-verb-text-make-head-rev"),
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new("/Textures/Interface/Misc/job_icons.rsi"), "HeadRevolutionary"),
            Act = () =>
            {
                _revolutionaryRule.OnHeadRevAdmin(args.Target);
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-make-head-rev"),
        };
        args.Verbs.Add(headRev);

        Verb thief = new()
        {
            Text = Loc.GetString("admin-verb-text-make-thief"),
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Clothing/Hands/Gloves/ihscombat.rsi"), "icon"),
            Act = () =>
            {
                _thief.AdminMakeThief(args.Target, false); //Midround add pacified is bad
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-make-thief"),
        };
        args.Verbs.Add(thief);
    }
}
