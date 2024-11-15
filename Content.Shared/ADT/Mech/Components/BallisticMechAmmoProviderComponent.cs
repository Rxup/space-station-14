using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Audio;

namespace Content.Shared.ADT.Weapons.Ranged.Components;

/// <summary>
/// Позволяет оружию меха стрелять проджектайлами.
/// Использует специальные магазины, которые помещаются в хранилище меха.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BallisticMechAmmoProviderComponent : MechAmmoProviderComponent
{
    [ViewVariables(VVAccess.ReadWrite), DataField("proto", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string Prototype = default!;

    [DataField]
    [AutoNetworkedField]
    public int Shots = 30;

    [DataField]
    public int Capacity = 30;

    [DataField]
    public float ReloadTime = 10f;

    [ViewVariables]
    public TimeSpan ReloadEnd = TimeSpan.Zero;

    [ViewVariables]
    public bool Reloading = false;

    [DataField]
    public string AmmoContainerId = "storagebase";

    [DataField]
    public string AmmoType = "lightrifle";

    [DataField]
    public SoundSpecifier NoAmmoForReload = new SoundPathSpecifier("/Audio/Machines/Nuke/angry_beep.ogg", new AudioParams().WithVolume(-3f));

    [DataField]
    public SoundSpecifier ReloadSound = new SoundPathSpecifier("/Audio/Mecha/sound_mecha_hydraulic.ogg");
}
