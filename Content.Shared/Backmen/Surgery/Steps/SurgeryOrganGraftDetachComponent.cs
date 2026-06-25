using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery.Steps;

/// <summary>
/// Detaches an arachne graft organ. Only valid on the last-attached graft segment (reverse install order).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryOrganGraftDetachComponent : Component;
