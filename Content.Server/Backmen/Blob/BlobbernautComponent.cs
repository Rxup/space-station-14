using Content.Shared.Backmen.Blob;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;

namespace Content.Server.Backmen.Blob;

[RegisterComponent]
public sealed partial class BlobbernautComponent : SharedBlobbernautComponent
{
    [ViewVariables(VVAccess.ReadWrite), DataField("damageFrequency")]
    public float DamageFrequency = 5;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextDamage = TimeSpan.Zero;

    [ViewVariables(VVAccess.ReadOnly), DataField("damage")]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new Dictionary<string, FixedPoint2>
        {
            { "Piercing", 25 },
        }
    };

    [ViewVariables(VVAccess.ReadOnly)]
    public bool IsDead = false;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? Factory = default!;
}
