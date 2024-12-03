using Content.Server.Backmen.Abilities.Xeno.Abilities;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Actions;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Backmen.Abilities.Xeno;

public sealed class XenoAbilitiesSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly GunSystem _gunSystem = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoAcidSpillerComponent, XenoAcidSpitActionEvent>(OnAcidSpit);
        SubscribeLocalEvent<XenoAcidSpillerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<XenoAcidSpillerComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnAcidSpit(EntityUid uid, XenoAcidSpillerComponent component, XenoAcidSpitActionEvent args)
    {
        if (args.Handled)
            return;

        var xform = Transform(uid);
        var acidBullet = Spawn(component.BulletSpawnId, xform.Coordinates);
        var mapCoords = _transform.ToMapCoordinates(args.Target);
        var direction = mapCoords.Position -  _transform.GetMapCoordinates(xform).Position;
        var userVelocity = _physics.GetMapLinearVelocity(uid);

        _gunSystem.ShootProjectile(acidBullet, direction, userVelocity, uid, uid);
        _audioSystem.PlayPvs(component.BulletSound, uid, component.BulletSound.Params);

        args.Handled = true;
    }

    private void OnStartup(EntityUid uid, XenoAcidSpillerComponent component, ComponentStartup args)
    {
        _actions.AddAction(uid, ref component.AcidSpitAction, component.AcidSpitActionId);
    }

    private void OnShutdown(EntityUid uid, XenoAcidSpillerComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.AcidSpitAction);
    }
}
