using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Body.OrganRelations;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(OrganRelationSystem))]
public sealed partial class ChildOrganComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Parent;
}
