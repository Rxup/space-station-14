using Content.Shared.Antag;
using Content.Shared.Backmen.Blob;
using Content.Shared.Backmen.Vampiric;
using Content.Shared.Ghost;
using Content.Shared.StatusIcon.Components;

namespace Content.Client.Backmen.Vampiric;

public sealed class BloodSuckerSystem : SharedBloodSuckerSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BkmVampireComponent, CanDisplayStatusIconsEvent>(OnCanShowVampireIcon);
    }

    private void OnCanShowVampireIcon<T>(EntityUid uid, T comp, ref CanDisplayStatusIconsEvent args) where T : IAntagStatusIconComponent
    {
        args.Cancelled = !CanDisplayIcon(args.User, comp.IconVisibleToGhost);
    }

    /// <summary>
    /// The criteria that determine whether a client should see Rev/Head rev icons.
    /// </summary>
    private bool CanDisplayIcon(EntityUid? uid, bool visibleToGhost)
    {
        if (visibleToGhost && HasComp<GhostComponent>(uid))
            return true;

        return HasComp<BkmVampireComponent>(uid);
    }
}
