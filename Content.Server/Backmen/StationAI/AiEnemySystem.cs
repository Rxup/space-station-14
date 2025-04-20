using Content.Shared.Backmen.StationAI;
using Content.Shared.Backmen.StationAI.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;

namespace Content.Server.Backmen.StationAI;

public sealed class AiEnemySystem : SharedAiEnemySystem
 {
     [Dependency] private readonly NpcFactionSystem _faction = default!;


     public override void Initialize()
     {
         base.Initialize();

         SubscribeLocalEvent<AIEnemyNTComponent, MapInitEvent>(OnAdd);
         SubscribeLocalEvent<AIEnemyNTComponent, ComponentShutdown>(OnRemove);
     }



     protected override void ToggleEnemy(EntityUid u, EntityUid target)
     {
         if (!EntityQuery.HasComponent(u))
             return;

         if (HasComp<AIEnemyNTComponent>(target))
             RemCompDeferred<AIEnemyNTComponent>(target);
         else
             EnsureComp<AIEnemyNTComponent>(target).Source = u;
     }

     [ValidatePrototypeId<NpcFactionPrototype>]
     private const string AiEnemyFaction = "AiEnemy";

     private void OnRemove(Entity<AIEnemyNTComponent> ent, ref ComponentShutdown args)
     {
         if (TryComp<NpcFactionMemberComponent>(ent, out var npcFactionMemberComponent))
         {
             _faction.RemoveFaction((ent.Owner, npcFactionMemberComponent), AiEnemyFaction);
         }

     }

     private void OnAdd(Entity<AIEnemyNTComponent> ent, ref MapInitEvent args)
     {
         if (TryComp<NpcFactionMemberComponent>(ent, out var npcFactionMemberComponent))
         {
             _faction.AddFaction((ent.Owner,npcFactionMemberComponent), AiEnemyFaction);
         }
     }
 }
