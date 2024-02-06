using Content.Shared.Antag;
using Content.Shared.Backmen.Flesh;
using Content.Shared.Ghost;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Tag;

namespace Content.Client.Backmen.Flesh;

public sealed class FleshCultistSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tag = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FleshCultistComponent, CanDisplayStatusIconsEvent>(OnCanShowCultIcon);
    }

    private void OnCanShowCultIcon<T>(EntityUid uid, T comp, ref CanDisplayStatusIconsEvent args) where T : IAntagStatusIconComponent
    {
        args.Cancelled = !CanDisplayIcon(args.User, comp.IconVisibleToGhost);
    }

    [ValidatePrototypeId<TagPrototype>]
    private const string TagFlesh = "Flesh";

    /// <summary>
    /// The criteria that determine whether a client should see Rev/Head rev icons.
    /// </summary>
    private bool CanDisplayIcon(EntityUid? uid, bool visibleToGhost)
    {
        if (HasComp<FleshCultistComponent>(uid))
            return true;

        if (visibleToGhost && HasComp<GhostComponent>(uid))
            return true;

        return uid.HasValue && _tag.HasTag(uid.Value, TagFlesh);
    }
}
