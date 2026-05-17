using Content.Server._Impstation.Revenant.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Server._Impstation.Revenant.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(RevenantAnimatedSystem))]
[AutoGenerateComponentPause]
public sealed partial class RevenantAnimatedComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? Revenant;

    public List<Component> AddedComponents = new();

    [AutoPausedField]
    public TimeSpan? EndTime;
}
