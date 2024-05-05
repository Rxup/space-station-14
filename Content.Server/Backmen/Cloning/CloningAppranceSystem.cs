using Content.Server.Administration.Commands;
using Content.Server.Backmen.Cloning.Components;
using Content.Server.Backmen.Cloning.Events;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Shared.Bed.Cryostorage;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Serialization.Manager;

namespace Content.Server.Backmen.Cloning;

public sealed class CloningAppearanceSystem : EntitySystem
{
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly StationSystem _stations = default!;
    [Dependency] private readonly StationSpawningSystem _spawning = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookupSystem = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CloningAppearanceComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<CloningAppearanceEvent>(OnPlayerSpawn);
    }

    private void OnPlayerSpawn(CloningAppearanceEvent ev)
    {
        var profile = _ticker.GetPlayerProfile(ev.Player);
        var mobUid = _spawning.SpawnPlayerMob(ev.Coords, null, profile, ev.StationUid);
        var targetMind = _mindSystem.GetOrCreateMind(ev.Player.UserId);

        foreach (var entry in ev.Component.Components.Values)
        {
            var comp = (Component) _serialization.CreateCopy(entry.Component, notNullableOverride: true);
            EntityManager.AddComponent(mobUid, comp, true);
        }

        if (ev.Component.Gear != null)
        {
            SetOutfitCommand.SetOutfit(mobUid, ev.Component.Gear, EntityManager);
        }

        foreach (var nearbyEntity in _entityLookupSystem.GetEntitiesInRange(mobUid, 1f))
        {
            if (!TryComp<CryostorageComponent>(nearbyEntity, out var cryostorageComponent))
                continue;

            if(!_container.TryGetContainer(nearbyEntity, cryostorageComponent.ContainerId, out var container))
                continue;

            if (!_container.CanInsert(mobUid, container, true))
                continue;

            _container.Insert(mobUid, container);
            break;
        }

        _mindSystem.TransferTo(targetMind, mobUid);
    }

    private void OnPlayerAttached(Entity<CloningAppearanceComponent> ent, ref PlayerAttachedEvent args)
    {
        if(TerminatingOrDeleted(ent))
            return;
        QueueLocalEvent(new CloningAppearanceEvent
        {
            Player = args.Player,
            Component = ent.Comp,
            StationUid = _stations.GetOwningStation(ent),
            Coords = Transform(ent).Coordinates
        });
        Del(ent);
    }
}
