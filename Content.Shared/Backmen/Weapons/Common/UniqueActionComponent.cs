using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Weapons.Common;

[RegisterComponent, NetworkedComponent]
[Access(typeof(UniqueActionSystem))]
public sealed partial class UniqueActionComponent : Component;
