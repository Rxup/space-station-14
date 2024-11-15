using Content.Shared.Weapons.Ranged;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.ADT.Weapons.Ranged.Components;

/// <summary>
/// Позволяет оружию меха стрелять хитсканом.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HitscanMechAmmoProviderComponent : MechAmmoProviderComponent
{
    [DataField("fireCost")]
    [AutoNetworkedField]
    public float ShotCost = 15f;

    [DataField(required: true)]
    public ProtoId<HitscanPrototype> Proto;
}
