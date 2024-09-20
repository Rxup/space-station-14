using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Standing;
using Content.Shared.Rotation;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server.Backmen.Standing;

public sealed class LayingDownSystem : SharedLayingDownSystem // WD EDIT
{
    [Dependency] private readonly INetConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        //SubscribeNetworkEvent<CheckAutoGetUpEvent>(OnCheckAutoGetUp);
    }

    public override void AutoGetUp(Entity<LayingDownComponent> ent)
    {
        if(!TryComp<EyeComponent>(ent, out var eyeComp) || !TryComp<RotationVisualsComponent>(ent, out var rotationVisualsComp))
            return;

        var xform = Transform(ent);

        var rotation = xform.LocalRotation + (eyeComp.Rotation - (xform.LocalRotation - _transform.GetWorldRotation(xform)));

        if (rotation.GetDir() is Direction.SouthEast or Direction.East or Direction.NorthEast or Direction.North)
        {
            rotationVisualsComp.HorizontalRotation = Angle.FromDegrees(270);
            return;
        }

        rotationVisualsComp.HorizontalRotation = Angle.FromDegrees(90);
    }

    protected override bool GetAutoGetUp(Entity<LayingDownComponent> ent, ICommonSession session)
    {
        return _cfg.GetClientCVar(session.Channel, CCVars.AutoGetUp);
    }
/*
    private void OnCheckAutoGetUp(CheckAutoGetUpEvent ev, EntitySessionEventArgs args)
    {
        var uid = GetEntity(ev.User);

        if (!TryComp(uid, out LayingDownComponent? layingDown))
            return;

        layingDown.AutoGetUp = _cfg.GetClientCVar(args.SenderSession.Channel, CCVars.AutoGetUp);
        Dirty(uid, layingDown);
    }
    */
}
