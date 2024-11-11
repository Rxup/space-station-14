using System.Numerics;
using JetBrains.Annotations;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Camera;

[UsedImplicitly]
public abstract class SharedCameraRecoilSystem : EntitySystem
{
    /// <summary>
    ///     Maximum rate of magnitude restore towards 0 kick.
    /// </summary>
    private const float RestoreRateMax = 30f;

    /// <summary>
    ///     Minimum rate of magnitude restore towards 0 kick.
    /// </summary>
    private const float RestoreRateMin = 0.1f;

    /// <summary>
    ///     Time in seconds since the last kick that lerps RestoreRateMin and RestoreRateMax
    /// </summary>
    private const float RestoreRateRamp = 4f;

    /// <summary>
    ///     The maximum magnitude of the kick applied to the camera at any point.
    /// </summary>
    protected const float KickMagnitudeMax = 1f;

    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly INetManager _net = default!;


    public override void Initialize()
    {
        SubscribeLocalEvent<CameraRecoilComponent, GetEyeOffsetEvent>(OnCameraRecoilGetEyeOffset);
    }

    private void OnCameraRecoilGetEyeOffset(Entity<CameraRecoilComponent> ent, ref GetEyeOffsetEvent args)
    {
        args.Offset += ent.Comp.BaseOffset + ent.Comp.CurrentKick;
    }

    /// <summary>
    ///     Applies explosion/recoil/etc kickback to the view of the entity.
    /// </summary>
    /// <remarks>
    ///     If the entity is missing <see cref="CameraRecoilComponent" /> and/or <see cref="EyeComponent" />,
    ///     this call will have no effect. It is safe to call this function on any entity.
    /// </remarks>
    public abstract void KickCamera(
        EntityUid euid,
        Vector2 kickback,
        CameraRecoilComponent? component = null,
        float? kickMagnitudeMax = null // backmen: KickMagnitudeMax
        );

    private void UpdateEyes(float frameTime)
    {
        var query = AllEntityQuery<CameraRecoilComponent, EyeComponent>();

        while (query.MoveNext(out var uid, out var recoil, out var eye))
        {
            var magnitude = recoil.CurrentKick.Length();
            if (magnitude <= 0.005f)
            {
                recoil.CurrentKick = Vector2.Zero;
            }
            else // Continually restore camera to 0.
            {
                var normalized = recoil.CurrentKick.Normalized();
                recoil.LastKickTime += frameTime;
                if (recoil.LastKickTime > RestoreRateRamp) // backmen start
                {
                    recoil.LastKickTime = RestoreRateRamp;
                }
                var restoreRate = MathHelper.Lerp(RestoreRateMin, RestoreRateMax, recoil.LastKickTime / RestoreRateRamp); // backmen end
                var restore = normalized * restoreRate * frameTime;
                var (x, y) = recoil.CurrentKick - restore;
                if (double.IsNaN(x) || Math.Sign(x) != Math.Sign(recoil.CurrentKick.X)) // backmen start
                    x = 0;
                if (double.IsNaN(y) || Math.Sign(y) != Math.Sign(recoil.CurrentKick.Y))
                    y = 0; // backmen end
                recoil.CurrentKick = new Vector2(x, y);
            }

            if (recoil.CurrentKick == recoil.LastKick)
                continue;

            recoil.LastKick = recoil.CurrentKick;
            var ev = new GetEyeOffsetEvent();
            RaiseLocalEvent(uid, ref ev);
            _eye.SetOffset(uid, ev.Offset, eye);
        }
    }

    public override void Update(float frameTime)
    {
        if (_net.IsServer)
            UpdateEyes(frameTime);
    }

    public override void FrameUpdate(float frameTime)
    {
        UpdateEyes(frameTime);
    }
}

[Serializable]
[NetSerializable]
public sealed class CameraKickEvent : EntityEventArgs
{
    public readonly NetEntity NetEntity;
    public readonly Vector2 Recoil;

    public CameraKickEvent(NetEntity netEntity, Vector2 recoil)
    {
        Recoil = recoil;
        NetEntity = netEntity;
    }
}
