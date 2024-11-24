using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.AccessWeaponBlockerSystem;

[NetworkedComponent]
public abstract partial class SharedAccessWeaponBlockerComponent : Component
{

}

[Serializable, NetSerializable]
public sealed class AccessWeaponBlockerComponentState : ComponentState
{
    public bool CanUse;
    public string AlertText = "";
}
