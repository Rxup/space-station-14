using Content.Shared.DoAfter;

namespace Content.Server.ADT.Mech.Components;

/// <summary>
/// Данный компонент имеют все мобы, он даёт им возможность выбраться из клешни меха.
/// </summary>
[RegisterComponent]
public sealed partial class CanEscapeMechComponent : Component
{
    public bool IsEscaping => DoAfter != null;

    [DataField("doAfter")]
    public DoAfterId? DoAfter;
}
