using Content.Shared.Backmen.VovaMech;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    // start-backmen: vova-mech-gun-holder
    /// <summary>
    /// Entity to use for ranged weapon origin when the user is piloting a vehicle.
    /// </summary>
    protected EntityUid GetShootOrigin(EntityUid user) => GetGunHandsHolder(user);

    protected EntityUid GetGunHandsHolder(EntityUid entity)
    {
        var ev = new GetGunHandsHolderEvent(entity);
        RaiseLocalEvent(entity, ref ev, broadcast: true);
        return ev.Holder;
    }

    private (EntityUid FromEntity, EntityCoordinates FromCoordinates, EntityCoordinates SpawnCoordinates) GetShootAmmoContext(
        EntityUid user,
        Entity<GunComponent> gun)
    {
        var fromEntity = GetGunHandsHolder(user);
        var fromCoordinates = Transform(gun).Coordinates;
        // start-backmen: casing-eject-grid-parent
        var fromMap = TransformSystem.ToMapCoordinates(fromCoordinates);
        var spawnCoordinates = Maps.TryFindGridAt(fromMap, out var gridUid, out _)
            ? TransformSystem.WithEntityId(fromCoordinates, gridUid)
            : new EntityCoordinates(Maps.GetMapOrInvalid(fromMap.MapId), fromMap.Position);
        // end-backmen: casing-eject-grid-parent
        return (fromEntity, fromCoordinates, spawnCoordinates);
    }

    private EntityUid? GetProjectileShooter(EntityUid? user, EntityUid? gunUid)
    {
        var shooter = user ?? gunUid;
        if (user != null)
        {
            var shootOrigin = GetShootOrigin(user.Value);
            if (shootOrigin != user)
                shooter = shootOrigin;
        }

        return shooter;
    }
    // end-backmen: vova-mech-gun-holder

    private void PrepareEjectCartridge(EntityUid entity, TransformComponent xform)
    {
        TransformSystem.AttachToGridOrMap(entity, xform); // backmen: casing-eject-grid-parent
    }
}
