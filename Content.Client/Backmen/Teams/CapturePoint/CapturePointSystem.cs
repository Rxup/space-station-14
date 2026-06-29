using Content.Shared.Backmen.Teams.CapturePoint;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Backmen.Teams.CapturePoint;

public sealed partial class CapturePointSystem : SharedCapturePointSystem
{
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private MetaDataSystem _metadata = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new CapturePointOverlay(EntityManager, _prototype, _gameTiming, _player));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<CapturePointOverlay>();
    }
}
