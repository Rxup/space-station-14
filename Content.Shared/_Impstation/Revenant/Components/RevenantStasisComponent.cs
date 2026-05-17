using Robust.Shared.GameStates;

namespace Content.Shared._Impstation.Revenant.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class RevenantStasisComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid Revenant;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan StasisDuration = TimeSpan.FromSeconds(120);

    public RevenantStasisComponent(TimeSpan stasisDuration, EntityUid revenant)
    {
        StasisDuration = stasisDuration;
        Revenant = revenant;
    }

    public RevenantStasisComponent()
    {
    }
}
