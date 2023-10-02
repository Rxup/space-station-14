using Content.Server.Backmen.Shipwrecked.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Zombies;
using Content.Shared.Humanoid;
using Content.Shared.Tools.Components;
using Content.Shared.Zombies;
using Robust.Server.GameObjects;
using Robust.Server.Physics;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Shipwrecked;

public sealed class NPCZombieSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly NpcFactionSystem _npcFactonSystem = default!;
    [Dependency] private readonly JointSystem _jointSystem = default!;
    [Dependency] private readonly NPCSystem _npcSystem = default!;
    [Dependency] private readonly ZombieSystem _zombieSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZombifiedOnSpawnComponent, ComponentStartup>(OnSpawnZombifiedStartup);
        SubscribeLocalEvent<ZombieSurpriseComponent, ComponentInit>(OnZombieSurpriseInit);
        SubscribeLocalEvent<ZombieWakeupOnTriggerComponent, TriggerEvent>(OnZombieWakeupTrigger);
    }

    private void OnSpawnZombifiedStartup(EntityUid uid, ZombifiedOnSpawnComponent component, ComponentStartup args)
    {
        RemCompDeferred<ZombifiedOnSpawnComponent>(uid);
        _zombieSystem.ZombifyEntity(uid);
    }

    [ValidatePrototypeId<EntityPrototype>] private const string ZombieSurpriseDetector = "ZombieSurpriseDetector";
    private void OnZombieSurpriseInit(EntityUid uid, ZombieSurpriseComponent component, ComponentInit args)
    {
        // Spawn a separate collider attached to the entity.
        var trigger = Spawn(ZombieSurpriseDetector, Transform(uid).Coordinates);
        Comp<ZombieWakeupOnTriggerComponent>(trigger).ToZombify = uid;
    }

    private void OnZombieWakeupTrigger(EntityUid uid, ZombieWakeupOnTriggerComponent component, TriggerEvent args)
    {
        QueueDel(uid);

        var toZombify = component.ToZombify;
        if (toZombify == null || Deleted(toZombify))
            return;

        _zombieSystem.ZombifyEntity(toZombify.Value);
    }
}
