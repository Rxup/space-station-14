using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Weapons.Ranged;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedPumpActionSystem))]
public sealed partial class PumpActionComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Pumped;

    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Weapons/Guns64/Shotguns/shotgun_cmb_pump.ogg");
}
