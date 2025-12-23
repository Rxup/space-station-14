using Robust.Shared.GameStates;

namespace Content.Shared._Backmen.Weapons.Common;

[RegisterComponent, NetworkedComponent]
[Access(typeof(UniqueActionSystem))]
public sealed partial class UniqueActionComponent : Component;
