using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Blob.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true, raiseAfterAutoHandleState: true), Access(typeof(SharedBlobbernautSystem))]
public sealed partial class BlobbernautComponent : Component
{
    [DataField("color"), AutoNetworkedField]
    [Access(Other = AccessPermissions.ReadWrite)]
    public Color Color = Color.White;

    [ViewVariables(VVAccess.ReadWrite), DataField("damageFrequency")]
    public float DamageFrequency = 5;

    [ViewVariables(VVAccess.ReadOnly)]
    public float NextDamage = 0;

    [ViewVariables(VVAccess.ReadOnly), DataField("damage")]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
        {
            { "Piercing", 25 },
        }
    };

    [ViewVariables(VVAccess.ReadOnly)]
    [Access(Other = AccessPermissions.ReadWrite)]
    public EntityUid? Factory = default!;
}
