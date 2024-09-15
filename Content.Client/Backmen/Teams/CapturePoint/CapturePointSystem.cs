using Content.Shared.Backmen.Teams.CapturePoint;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Backmen.Teams.CapturePoint;

public sealed class CapturePointSystem : SharedCapturePointSystem
{
    [Dependency] protected readonly IGameTiming GameTiming = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new CapturePointOverlay(EntityManager, _prototype, GameTiming, _player));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<CapturePointOverlay>();
    }
}
