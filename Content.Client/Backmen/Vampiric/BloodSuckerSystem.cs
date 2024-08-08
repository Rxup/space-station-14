using Content.Shared.Antag;
using Content.Shared.Backmen.Blob;
using Content.Shared.Backmen.Vampiric;
using Content.Shared.Ghost;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Backmen.Vampiric;

public sealed class BloodSuckerSystem : SharedBloodSuckerSystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private StatusIconPrototype _statusIconPrototype = default!;
    public override void Initialize()
    {
        base.Initialize();

        _statusIconPrototype = _prototype.Index<StatusIconPrototype>(VampireFaction);
        SubscribeLocalEvent<BkmVampireComponent, GetStatusIconsEvent>(OnShowVampireIcon);
    }

    [ValidatePrototypeId<StatusIconPrototype>]
    private const string VampireFaction = "VampireFaction";

    private void OnShowVampireIcon(Entity<BkmVampireComponent> ent, ref GetStatusIconsEvent args)
    {
        args.StatusIcons.Add(_statusIconPrototype);
    }
}
