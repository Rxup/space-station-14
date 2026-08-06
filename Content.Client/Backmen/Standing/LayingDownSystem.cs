using System.Collections.Generic;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Standing;
using Content.Shared.Buckle;
using Content.Shared.Rotation;
using Content.Shared.Standing;
using Robust.Client.GameObjects;
using Robust.Client.GameStates;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Network.Messages;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.Backmen.Standing;

public sealed partial class LayingDownSystem : SharedLayingDownSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedRotationVisualsSystem _rotationVisuals = default!;
    [Dependency] private SpriteSystem _sprites = default!;
    [Dependency] private IClientGameStateManager _gameState = default!;

    private bool _autoGetUp;

    /// <summary>
    /// Entities that left PVS with a possibly frozen rotate anim / stale sprite pose.
    /// Next standing-state apply must force a visual rebuild instead of yielding to "rotate".
    /// </summary>
    private readonly HashSet<EntityUid> _pvsVisualRebuild = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LayingDownComponent, MoveEvent>(OnMovementInput);
        SubscribeLocalEvent<StandingStateComponent, AfterAutoHandleStateEvent>(OnChangeStanding);

        _cfg.OnValueChanged(CCVars.AutoGetUp, b => _autoGetUp = b, true);
        _gameState.PvsLeave += OnPvsLeave;

        //SubscribeNetworkEvent<CheckAutoGetUpEvent>(OnCheckAutoGetUp);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _gameState.PvsLeave -= OnPvsLeave;
    }

    private void OnPvsLeave(MsgStateLeavePvs message)
    {
        foreach (var netEntity in message.Entities)
        {
            if (!TryGetEntity(netEntity, out var uid))
                continue;

            // Detach pauses the entity and freezes in-flight "rotate" playbacks.
            // Clear them now so prediction/Appearance aren't fighting a stale anim on re-entry.
            if (_animation.HasRunningAnimation(uid.Value, "rotate"))
                _animation.Stop(uid.Value, null, "rotate");

            _pvsVisualRebuild.Add(uid.Value);
        }
    }

    private void OnChangeStanding(Entity<StandingStateComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var forceRebuild = _pvsVisualRebuild.Remove(ent.Owner);

        // Prediction re-applies StandingState every tick. Yielding to an in-progress Appearance
        // "rotate" anim avoids stopping/restarting it (which looks like flapping).
        // After PVS leave we must force-correct even if a stale key is still present.
        if (!forceRebuild && _animation.HasRunningAnimation(ent, "rotate"))
            return;

        if (forceRebuild && _animation.HasRunningAnimation(ent, "rotate"))
            _animation.Stop(ent.Owner, null, "rotate");

        if (ent.Comp.Standing)
        {
            var vertical = Angle.Zero;
            if (TryComp<RotationVisualsComponent>(ent, out var standingRotation))
                vertical = standingRotation.VerticalRotation;

            _sprites.SetRotation((ent, sprite), vertical);
            return;
        }

        var horizontal = Angle.FromDegrees(270);
        if (TryComp<RotationVisualsComponent>(ent, out var lyingRotation))
            horizontal = lyingRotation.HorizontalRotation;

        if (sprite.Rotation.Equals(horizontal)
            || sprite.Rotation.Equals(Angle.FromDegrees(90))
            || sprite.Rotation.Equals(Angle.FromDegrees(270)))
            return;

        _sprites.SetRotation((ent, sprite), horizontal);
    }

    protected override bool GetAutoGetUp(Entity<LayingDownComponent> ent, ICommonSession session)
    {
        return _autoGetUp;
    }

    private void OnMovementInput(EntityUid uid, LayingDownComponent component, MoveEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if(!_standing.IsDown(uid) || _animation.HasRunningAnimation(uid, "rotate") || _buckle.IsBuckled(uid))
            return;

        if(TerminatingOrDeleted(uid))
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite)
            || !TryComp<RotationVisualsComponent>(uid, out var rotationVisuals))
        {
            return;
        }

        ProcessVisuals((uid, Transform(uid), sprite, rotationVisuals));
    }

    private void ProcessVisuals(Entity<TransformComponent, SpriteComponent?, RotationVisualsComponent> entity)
    {
        var rotation = entity.Comp1.LocalRotation + (_eyeManager.CurrentEye.Rotation - (entity.Comp1.LocalRotation - _transform.GetWorldRotation(entity.Comp1)));

        if (rotation.GetDir() is Direction.SouthEast or Direction.East or Direction.NorthEast or Direction.North)
        {
            _rotationVisuals.SetHorizontalAngle((entity.Owner, entity.Comp3), Angle.FromDegrees(270));
            if (entity.Comp2 != null)
                _sprites.SetRotation((entity.Owner, entity.Comp2), Angle.FromDegrees(270));
            return;
        }

        _rotationVisuals.ResetHorizontalAngle((entity.Owner, entity.Comp3));
        if (entity.Comp2 != null)
            _sprites.SetRotation((entity.Owner, entity.Comp2), entity.Comp3.DefaultRotation);
    }

    public override void AutoGetUp(Entity<LayingDownComponent> ent)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (TerminatingOrDeleted(ent))
            return;

        var transform = Transform(ent);

        if (!TryComp<RotationVisualsComponent>(ent, out var rotationVisuals))
            return;

        ProcessVisuals((ent.Owner, transform, null, rotationVisuals));
    }

    /*
    private void OnCheckAutoGetUp(CheckAutoGetUpEvent ev, EntitySessionEventArgs args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var uid = GetEntity(ev.User);

        if (!TryComp<TransformComponent>(uid, out var transform) || !TryComp<RotationVisualsComponent>(uid, out var rotationVisuals))
            return;

        var rotation = transform.LocalRotation + (_eyeManager.CurrentEye.Rotation - (transform.LocalRotation - transform.WorldRotation));

        if (rotation.GetDir() is Direction.SouthEast or Direction.East or Direction.NorthEast or Direction.North)
        {
            rotationVisuals.HorizontalRotation = Angle.FromDegrees(270);
            return;
        }

        rotationVisuals.HorizontalRotation = Angle.FromDegrees(90);
    }*/
}
