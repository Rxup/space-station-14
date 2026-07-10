using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared.Backmen.VentCrawler;

/// <summary>
/// Active state while an entity is crawling through vents.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedVentCrawlerSystem))]
public sealed partial class VentCrawlingComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid CurrentPipe;

    [DataField, AutoNetworkedField]
    public bool IsStepping;

    [ViewVariables]
    public float StepTimeLeft;

    [ViewVariables]
    public float StepStartingTime;

    [ViewVariables]
    public EntityCoordinates StepOrigin;

    [ViewVariables]
    public EntityCoordinates StepDestination;

    [ViewVariables]
    public int? OriginalDrawDepth;

    [ViewVariables]
    public bool HadVisibility;

    [DataField, AutoNetworkedField]
    public EntityUid? ExitActionEntity;

    [ViewVariables]
    public bool AppliedPressureImmunity;

    [ViewVariables]
    public float? SavedAtmosTemperatureTransferEfficiency;
}
