using Content.Server.Administration.Commands;
using Content.Server.Antag;
using Content.Server.Backmen.GameTicking.Rules.Components;
using Content.Server.Backmen.Vampiric;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Zombies;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Administration.Systems;

public sealed partial class AdminVerbSystem
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly ZombieSystem _zombie = default!;

    [ValidatePrototypeId<EntityPrototype>]
    private const string DefaultTraitorRule = "Traitor";

    [ValidatePrototypeId<EntityPrototype>]
    private const string DefaultInitialInfectedRule = "Zombie";

    [ValidatePrototypeId<EntityPrototype>]
    private const string DefaultNukeOpRule = "LoneOpsSpawn";

    [ValidatePrototypeId<EntityPrototype>]
    private const string DefaultRevsRule = "Revolutionary";

    [ValidatePrototypeId<EntityPrototype>]
    private const string DefaultThiefRule = "Thief";

    [ValidatePrototypeId<StartingGearPrototype>]
    private const string PirateGearId = "PirateGear";

    // All antag verbs have names so invokeverb works.
    private void AddAntagVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        var player = actor.PlayerSession;

        if (!_adminManager.HasAdminFlag(player, AdminFlags.Fun))
            return;

        if (!HasComp<MindContainerComponent>(args.Target) || !TryComp<ActorComponent>(args.Target, out var targetActor))
            return;

        var targetPlayer = targetActor.PlayerSession;

        var traitorName = Loc.GetString("admin-verb-text-make-traitor");
        Verb traitor = new()
        {
            Text = traitorName,
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Interface/Misc/job_icons.rsi"), "Syndicate"),
            Act = () =>
            {
                _antag.ForceMakeAntag<TraitorRuleComponent>(targetPlayer, DefaultTraitorRule);
            },
            Impact = LogImpact.High,
            Message = string.Join(": ", traitorName,  Loc.GetString("admin-verb-make-traitor")),
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

        Verb vampireAntag = new()
        {
            Text = Loc.GetString("admin-verb-text-make-vampire"),
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Texture(new ("/Textures/Backmen/Icons/verbiconfangs.png")),
            Act = () =>
            {
                if (!HasComp<ActorComponent>(args.Target))
                    return;

                EntityManager.System<BloodSuckerSystem>().ForceMakeVampire(args.Target);
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-text-make-vampire"),
        };
        args.Verbs.Add(vampireAntag);

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
        var initialInfectedName = Loc.GetString("admin-verb-text-make-initial-infected");
        Verb initialInfected = new()
        {
            Text = initialInfectedName,
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new("/Textures/Interface/Misc/job_icons.rsi"), "InitialInfected"),
            Act = () =>
            {
                _antag.ForceMakeAntag<ZombieRuleComponent>(targetPlayer, DefaultInitialInfectedRule);
            },
            Impact = LogImpact.High,
            Message = string.Join(": ", initialInfectedName, Loc.GetString("admin-verb-make-initial-infected")),
        };
        args.Verbs.Add(initialInfected);

        var zombieName = Loc.GetString("admin-verb-text-make-zombie");
        Verb zombie = new()
        {
            Text = zombieName,
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new("/Textures/Interface/Misc/job_icons.rsi"), "Zombie"),
            Act = () =>
            {
                _zombie.ZombifyEntity(args.Target);
            },
            Impact = LogImpact.High,
            Message = string.Join(": ", zombieName, Loc.GetString("admin-verb-make-zombie")),
        };
        args.Verbs.Add(zombie);

        var nukeOpName = Loc.GetString("admin-verb-text-make-nuclear-operative");
        Verb nukeOp = new()
        {
            Text = nukeOpName,
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new("/Textures/Clothing/Head/Hardsuits/syndicate.rsi"), "icon"),
            Act = () =>
            {
                _antag.ForceMakeAntag<NukeopsRuleComponent>(targetPlayer, DefaultNukeOpRule);
            },
            Impact = LogImpact.High,
            Message = string.Join(": ", nukeOpName, Loc.GetString("admin-verb-make-nuclear-operative")),
        };
        args.Verbs.Add(nukeOp);

        var pirateName = Loc.GetString("admin-verb-text-make-pirate");
        Verb pirate = new()
        {
            Text = pirateName,
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new("/Textures/Clothing/Head/Hats/pirate.rsi"), "icon"),
            Act = () =>
            {
                // pirates just get an outfit because they don't really have logic associated with them
                SetOutfitCommand.SetOutfit(args.Target, PirateGearId, EntityManager);
            },
            Impact = LogImpact.High,
            Message = string.Join(": ", pirateName, Loc.GetString("admin-verb-make-pirate")),
        };
        args.Verbs.Add(pirate);

        var headRevName = Loc.GetString("admin-verb-text-make-head-rev");
        Verb headRev = new()
        {
            Text = headRevName,
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new("/Textures/Interface/Misc/job_icons.rsi"), "HeadRevolutionary"),
            Act = () =>
            {
                _antag.ForceMakeAntag<RevolutionaryRuleComponent>(targetPlayer, DefaultRevsRule);
            },
            Impact = LogImpact.High,
            Message = string.Join(": ", headRevName, Loc.GetString("admin-verb-make-head-rev")),
        };
        args.Verbs.Add(headRev);

        var thiefName = Loc.GetString("admin-verb-text-make-thief");
        Verb thief = new()
        {
            Text = thiefName,
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Clothing/Hands/Gloves/Color/black.rsi"), "icon"),
            Act = () =>
            {
                _antag.ForceMakeAntag<ThiefRuleComponent>(targetPlayer, DefaultThiefRule);
            },
            Impact = LogImpact.High,
            Message = string.Join(": ", thiefName, Loc.GetString("admin-verb-make-thief")),
        };
        args.Verbs.Add(thief);

        //Changelings: start
        Verb ling = new()
        {
            Text = Loc.GetString("admin-verb-text-make-changeling"),
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Backmen/Changeling/changeling_abilities.rsi"), "transform"),
            Act = () =>
            {
                _antag.ForceMakeAntag<ChangelingRuleComponent>(targetPlayer, "Changeling");
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-make-changeling"),
        };
        args.Verbs.Add(ling);
        //Changelings: end
        // goobstation - heretics
        Verb heretic = new()
        {
            Text = Loc.GetString("admin-verb-make-heretic"),
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/ADT/Heretic/Blades/blade_blade.rsi"), "icon"),
            Act = () =>
            {
                _antag.ForceMakeAntag<HereticRuleComponent>(targetPlayer, "Heretic");
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-make-heretic"),
        };
        args.Verbs.Add(heretic);
    }
}
