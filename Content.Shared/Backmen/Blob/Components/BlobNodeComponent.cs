using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Blob.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlobNodeComponent : Component
{
    [DataField]
    public float PulseFrequency = 4f;

    [DataField]
    public float PulseRadius = 4f;

    public TimeSpan NextPulse = TimeSpan.Zero;

    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? BlobResource = null;
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? BlobFactory = null;
    /*
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? BlobStorage = null;
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? BlobTurret = null;
    */
}

public sealed class BlobTileGetPulseEvent : HandledEntityEventArgs
{

}

public sealed class BlobMobGetPulseEvent : EntityEventArgs;

/// <summary>
/// Event raised on all special tiles of Blob Node on pulse.
/// </summary>
public sealed class BlobSpecialGetPulseEvent : EntityEventArgs;
public sealed class BlobNodePulseEvent : EntityEventArgs;
