using Content.Shared.Antag;
using Content.Shared.Backmen.Flesh;
using Content.Shared.Ghost;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Client.Backmen.Flesh;

public sealed class FleshCultistSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FleshCultistComponent, GetStatusIconsEvent>(OnShowCultIcon);
    }

    [ValidatePrototypeId<FactionIconPrototype>]
    private const string FleshcultistFaction = "FleshcultistFaction";

    private void OnShowCultIcon(Entity<FleshCultistComponent> ent, ref GetStatusIconsEvent args)
    {
        args.StatusIcons.Add(_prototype.Index<FactionIconPrototype>(FleshcultistFaction));
    }
}
