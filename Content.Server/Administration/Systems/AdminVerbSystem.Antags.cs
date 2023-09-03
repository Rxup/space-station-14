using Content.Server.GameTicking.Rules;
using Content.Server.Zombies;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Utility;

namespace Content.Server.Administration.Systems;

public sealed partial class AdminVerbSystem
{
    [Dependency] private readonly ZombieSystem _zombie = default!;
    [Dependency] private readonly TraitorRuleSystem _traitorRule = default!;
    [Dependency] private readonly NukeopsRuleSystem _nukeopsRule = default!;
    [Dependency] private readonly PiratesRuleSystem _piratesRule = default!;
    [Dependency] private readonly SharedMindSystem _minds = default!;

    // All antag verbs have names so invokeverb works.
    private void AddAntagVerbs(GetVerbsEvent<Verb> args)
    {
        if (!EntityManager.TryGetComponent(args.User, out ActorComponent? actor))
            return;

        var player = actor.PlayerSession;

        if (!_adminManager.HasAdminFlag(player, AdminFlags.Fun))
            return;

        var targetHasMind = TryComp(args.Target, out MindContainerComponent? targetMindComp);
        if (!targetHasMind || targetMindComp == null)
            return;

        Verb traitor = new()
        {
            Text = "Make Traitor",
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Structures/Wallmounts/posters.rsi"), "poster5_contraband"),
            Act = () =>
            {
                if (!_minds.TryGetSession(targetMindComp.Mind, out var session))
                    return;

                _traitorRule.MakeTraitor(session);
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-make-traitor"),
        };
        args.Verbs.Add(traitor);

        Verb EvilTwin = new()
        {
            Text = "Make EvilTwin",
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi((new ResPath("/Textures/Structures/Wallmounts/posters.rsi")), "poster3_legit"),
            Act = () =>
            {
                if (!_minds.TryGetSession(targetMindComp.Mind, out var session))
                    return;

                EntityManager.System<Content.Server.Backmen.EvilTwin.EvilTwinSystem>().MakeTwin(out _,session.AttachedEntity);
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-make-eviltwin"),
        };
        args.Verbs.Add(EvilTwin);

        Verb zombie = new()
        {
            Text = "Make Zombie",
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
            Text = "Make nuclear operative",
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new("/Textures/Structures/Wallmounts/signs.rsi"), "radiation"),
            Act = () =>
            {
                if (!_minds.TryGetMind(args.Target, out var mindId, out var mind))
                    return;

                _nukeopsRule.MakeLoneNukie(mindId, mind);
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-make-nuclear-operative"),
        };
        args.Verbs.Add(nukeOp);

        Verb pirate = new()
        {
            Text = "Make Pirate",
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new("/Textures/Clothing/Head/Hats/pirate.rsi"), "icon"),
            Act = () =>
            {
                if (!_minds.TryGetMind(args.Target, out var mindId, out var mind))
                    return;

                _piratesRule.MakePirate(mindId, mind);
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-make-pirate"),
        };
        args.Verbs.Add(pirate);

    }
}
