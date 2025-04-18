using System.Numerics;
using Content.Server.Popups;
using Content.Server.SurveillanceCamera;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Backmen.StationAI;
using Content.Shared.Backmen.StationAI.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.StationAI;

public sealed class AiEnemySystem : SharedAiEnemySystem
 {
     [Dependency] private readonly NpcFactionSystem _faction = default!;
     [Dependency] private readonly SharedTransformSystem _transform = default!;
     [Dependency] private readonly PopupSystem _popup = default!;
     [Dependency] private readonly GunSystem _gun = default!;
     [Dependency] private readonly SharedAudioSystem _audio = default!;
     [Dependency] private readonly MapSystem _map = default!;
     [Dependency] private readonly TagSystem _tag = default!;

     public override void Initialize()
     {
         base.Initialize();

         SubscribeLocalEvent<AIEnemyNTComponent, MapInitEvent>(OnAdd);
         SubscribeLocalEvent<AIEnemyNTComponent, ComponentShutdown>(OnRemove);
         SubscribeLocalEvent<StationAiCoreComponent, AIEyeCampShootActionEvent>(OnShoot);
         SubscribeLocalEvent<StationAiCoreComponent, AIEyeCampActionEvent>(OnOpenCamUi);
         SubscribeLocalEvent<StationAiCoreComponent, EyeMoveToCam>(OnMoveToCam);
     }

     private void OnOpenCamUi(Entity<StationAiCoreComponent> ent, ref AIEyeCampActionEvent args)
     {

     }

     private void OnMoveToCam(Entity<StationAiCoreComponent> ent, ref EyeMoveToCam args)
     {
         if (
             !TryGetEntity(args.Uid, out var uid) ||
             ent.Comp.RemoteEntity == null ||
             TerminatingOrDeleted(ent.Comp.RemoteEntity) ||
             !HasComp<AIEyeComponent>(ent.Comp.RemoteEntity)
             )
             return;

         var camPos = Transform(uid.Value);
         if (Transform(uid.Value).GridUid != Transform(ent).GridUid)
             return;

         if (!TryComp<SurveillanceCameraComponent>(uid, out var camera))
             return;

         if (!camera.Active)
         {
             _popup.PopupCursor("камера не работает!", ent, PopupType.LargeCaution);
             return;
         }
         _transform.SetCoordinates(ent.Comp.RemoteEntity.Value, camPos.Coordinates);
         _transform.AttachToGridOrMap(ent.Comp.RemoteEntity.Value);
     }

     [ValidatePrototypeId<EntityPrototype>]
     private const string BulletDisabler = "BulletDisabler";
     private void OnShoot(Entity<StationAiCoreComponent> ent, ref AIEyeCampShootActionEvent args)
     {
         if(ent.Comp.RemoteEntity == null || TerminatingOrDeleted(ent.Comp.RemoteEntity))
             return;

         if(!TryComp<AIEyeComponent>(ent.Comp.RemoteEntity, out var eye))
             return;

         if(eye.Camera == null || TerminatingOrDeleted(eye.Camera))
             return;

         if (_transform.GetGrid(args.Target) != Transform(ent).GridUid)
             return;

         if (!TryComp<SurveillanceCameraComponent>(eye.Camera, out var camera))
             return;

         if (!camera.Active)
         {
             _popup.PopupCursor("камера не работает!", ent.Comp.RemoteEntity.Value, PopupType.LargeCaution);
             return;
         }

         args.Handled = true;

         var targetPos = _transform.ToMapCoordinates(args.Target);
         var camPos = Transform(eye.Camera.Value).Coordinates;
         var camMapPos = _transform.ToMapCoordinates(camPos);

         var ammo = Spawn(BulletDisabler, camPos);
         _gun.ShootProjectile(ammo, targetPos.Position - camMapPos.Position, Vector2.One, eye.Camera.Value, args.Performer);
         _audio.PlayPvs("/Audio/Weapons/Guns/Gunshots/taser2.ogg", eye.Camera.Value);
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
