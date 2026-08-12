using Robust.Shared.GameStates;

namespace Content.Shared._Impstation.Revenant.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class RevenantStasisComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid Revenant;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan StasisDuration = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Set when ectoplasm is intentionally destroyed (salt grind or bible exorcism).
    /// If false and the ectoplasm is deleted anyway (broken blender, etc.), the revenant is revived.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool PermanentlyDestroyed;

    /// <summary>
    /// Prevents double-handling when we revive then delete the ectoplasm.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool Revived;

    public RevenantStasisComponent(TimeSpan stasisDuration, EntityUid revenant)
    {
        StasisDuration = stasisDuration;
        Revenant = revenant;
    }

    public RevenantStasisComponent()
    {
    }
}
