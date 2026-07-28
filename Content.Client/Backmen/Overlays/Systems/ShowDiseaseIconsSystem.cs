using Content.Client.Overlays;
using Content.Shared.Backmen.Disease;
using Content.Shared.Backmen.Overlays;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Backmen.Overlays.Systems;

/// <summary>
/// Shows a generic "infected" icon on the medical HUD when the target has <see cref="DiseasedComponent"/>.
/// Does not reveal which disease — that still requires swab/diagnoser workflow.
/// </summary>
public sealed partial class ShowDiseaseIconsSystem : EquipmentHudSystem<ShowDiseaseIconsComponent>
{
    private static readonly ProtoId<HealthIconPrototype> InfectedIcon = "InfectedIcon";

    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private HealthIconPrototype _infectedIcon = default!;

    public override void Initialize()
    {
        base.Initialize();

        CacheIcons();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<DiseasedComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<HealthIconPrototype>())
            CacheIcons();
    }

    private void CacheIcons()
    {
        _infectedIcon = _prototype.Index(InfectedIcon);
    }

    private void OnGetStatusIconsEvent(EntityUid uid, DiseasedComponent component, ref GetStatusIconsEvent ev)
    {
        if (!IsActive)
            return;

        ev.StatusIcons.Add(_infectedIcon);
    }
}
