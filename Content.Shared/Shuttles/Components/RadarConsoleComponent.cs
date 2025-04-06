using Content.Shared.Shuttles.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Shuttles.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRadarConsoleSystem))]
public sealed partial class RadarConsoleComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public float RangeVV
    {
        get => MaxRange;
        set => IoCManager
            .Resolve<IEntitySystemManager>()
            .GetEntitySystem<SharedRadarConsoleSystem>()
            .SetRange(Owner, value, this);
    }

    [DataField, AutoNetworkedField]
    public float MaxRange = 256f;

    // backmen edit start
    /// <summary>
    /// "It's over" THE TIMER
    /// bleep..... bleep....... bleeep.... bleeeep..... bleeep....
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan DetectablesUpdateRate = TimeSpan.FromSeconds(1.2f);

    [AutoNetworkedField, ViewVariables]
    public TimeSpan NextDetectableUpdate;

    /// <summary>
    /// The multiplier applied to the spottable distance of Detectable entities, use this if you have a big ass grid
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SpottingMultiplier = 1f;
    // backmen edit end

    /// <summary>
    /// If true, the radar will be centered on the entity. If not - on the grid on which it is located.
    /// </summary>
    [DataField]
    public bool FollowEntity = false;
}
