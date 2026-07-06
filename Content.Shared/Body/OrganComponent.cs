using Content.Shared.Backmen.Surgery.Tools;
using Content.Shared.Backmen.Surgery.Body.Organs;
using Content.Shared.Backmen.Surgery.Traumas.Systems;
using Content.Shared.Backmen.Body.Systems; // backmen: body
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
// start-backmen: body
[Access(typeof(BodySystem), typeof(BkmBodySharedSystem), typeof(TraumaSystem), typeof(OrganEffectSystem))] // backmen: space-animal-organs
// end-backmen: body
public sealed partial class OrganComponent : Component, ISurgeryToolComponent
{
    /// <summary>
    /// The body entity containing this organ, if any
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Body;

    /// <summary>
    /// What kind of organ is this, if any
    /// </summary>
    [DataField]
    public ProtoId<OrganCategoryPrototype>? Category;
}
