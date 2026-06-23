using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Body.OrganRelations;

[RegisterComponent, NetworkedComponent]
public sealed partial class DetachableOrganComponent : Component
{
    public bool Detaching;

    [DataField(required: true)]
    public EntProtoId DetachedBody;
}
