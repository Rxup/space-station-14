using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared.Backmen.StationAI;
using Content.Shared.Backmen.StationAI.Components;
using Content.Shared.Backmen.StationAI.Systems;
using Content.Shared.Examine;
using Content.Shared.Mobs.Systems;
using Content.Shared.Verbs;

namespace Content.Server.Backmen.StationAI.Systems;

public sealed class AiEnemySystem : SharedAiEnemySystem
{
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AIEnemyNTComponent, MapInitEvent>(OnAdd);
        SubscribeLocalEvent<AIEnemyNTComponent, ComponentShutdown>(OnRemove);
        SubscribeLocalEvent<NpcFactionMemberComponent, GetVerbsEvent<AlternativeVerb>>(OnMarkAsTarget);

    }

    private void OnMarkAsTarget(Entity<NpcFactionMemberComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!HasComp<StationAIComponent>(args.User) && !HasComp<BorgAINTComponent>(args.User))
            return;

        if (_mobState.IsDead(args.User))
            return;

        var u = args.User;
        AlternativeVerb verb = new()
        {
            Act = () =>
            {
                ToggleEnemy(u, ent);
            },
            Text = Loc.GetString("sai-enemy-verb"),
            Priority = 2
        };

        args.Verbs.Add(verb);
    }

    private void ToggleEnemy(EntityUid argsUser, Entity<NpcFactionMemberComponent> ent)
    {
        var core = argsUser;
        if (TryComp<AIEyeComponent>(core, out var eyeComponent))
        {
            if(eyeComponent.AiCore == null)
                return;
            core = eyeComponent.AiCore.Value.Owner;
        }

        if (!core.Valid)
        {
            return;
        }

        var xform = Transform(core);
        if (xform.GridUid != Transform(ent).GridUid || !xform.Anchored)
        {
            return;
        }

        if (HasComp<AIEnemyNTComponent>(ent))
            RemCompDeferred<AIEnemyNTComponent>(ent);
        else
            EnsureComp<AIEnemyNTComponent>(ent).Source = core;
    }

    [ValidatePrototypeId<NpcFactionPrototype>]
    private const string AiEnemyFaction = "AiEnemy";

    private void OnRemove(Entity<AIEnemyNTComponent> ent, ref ComponentShutdown args)
    {
        _faction.RemoveFaction(ent, AiEnemyFaction);
    }

    private void OnAdd(Entity<AIEnemyNTComponent> ent, ref MapInitEvent args)
    {
        _faction.AddFaction(ent, AiEnemyFaction);
    }
}
