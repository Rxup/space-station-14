using Robust.Shared.Audio;

namespace Content.Shared.Body;

public sealed partial class BodyComponent
{
    [DataField, AutoNetworkedField]
    public SoundSpecifier GibSound = new SoundCollectionSpecifier("gib");

    /// <summary>
    /// The amount of legs required to move at full speed.
    /// If 0, then legs do not impact speed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int RequiredLegs;

    [ViewVariables]
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> LegEntities = new();
}
