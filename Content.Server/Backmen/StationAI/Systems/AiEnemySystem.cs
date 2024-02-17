using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared.Backmen.StationAI;
using Content.Shared.Backmen.StationAI.Components;
using Content.Shared.Backmen.StationAI.Systems;
using Content.Shared.Examine;
using Content.Shared.Verbs;

namespace Content.Server.Backmen.StationAI.Systems;

public sealed class AiEnemySystem : SharedAiEnemySystem
{
    [Dependency] private readonly NpcFactionSystem _faction = default!;

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
        var source = argsUser;
        if (TryComp<AIEyeComponent>(argsUser, out var aiEyeComponent))
        {
            source = aiEyeComponent.AiCore?.Owner ?? EntityUid.Invalid;
        }
        if (HasComp<AIEnemyNTComponent>(ent))
            RemCompDeferred<AIEnemyNTComponent>(ent);
        else
            EnsureComp<AIEnemyNTComponent>(ent).Source = source;
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
