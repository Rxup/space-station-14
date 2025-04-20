using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.StationAI.Components;


[RegisterComponent, NetworkedComponent, AutoGenerateComponentState()]
public sealed partial class AICameraComponent : Component
{
    [DataField("enabled"), ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool Enabled = false;

    [DataField("cameraName"), ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public string CameraName = "Unnamed";

    [DataField("cameraCategory"), ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public List<string> CameraCategories = new List<string>()
    {
        "Uncategorized"
    };

    [ViewVariables]
    public HashSet<EntityUid> ActiveViewers { get; } = new();
}
