using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Standing;
using Content.Shared.Rotation;
using Content.Shared.Standing;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server.Backmen.Standing;

public sealed class LayingDownSystem : SharedLayingDownSystem
{
    [Dependency] private readonly INetConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedRotationVisualsSystem _rotationVisuals = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    protected override bool GetAutoGetUp(Entity<LayingDownComponent> ent, ICommonSession session)
    {
        return _cfg.GetClientCVar(session.Channel, CCVars.AutoGetUp);
    }

    public override void AutoGetUp(Entity<LayingDownComponent> ent)
    {
        if (!TryComp<EyeComponent>(ent, out var eyeComp) || !TryComp<RotationVisualsComponent>(ent, out var rotationVisualsComp))
            return;

        var xform = Transform(ent);
        var rotation = xform.LocalRotation + (eyeComp.Rotation - (xform.LocalRotation - _transform.GetWorldRotation(xform)));

        if (rotation.GetDir() is Direction.SouthEast or Direction.East or Direction.NorthEast or Direction.North)
        {
            _rotationVisuals.SetHorizontalAngle((ent, rotationVisualsComp), Angle.FromDegrees(270));
            return;
        }

        _rotationVisuals.ResetHorizontalAngle((ent, rotationVisualsComp));
    }
}
