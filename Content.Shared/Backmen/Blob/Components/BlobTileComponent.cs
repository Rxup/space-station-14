using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Blob.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), Serializable]
public sealed partial class BlobTileComponent : Component
{
    [DataField("color"), AutoNetworkedField]
    public Color Color = Color.White;

    [ViewVariables(VVAccess.ReadOnly)]
    public Entity<BlobCoreComponent>? Core;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool ReturnCost = true;

    [ViewVariables(VVAccess.ReadOnly), DataField("tileType")]
    public BlobTileType BlobTileType = BlobTileType.Normal;

    [ViewVariables(VVAccess.ReadOnly), DataField("healthOfPulse")]
    public DamageSpecifier HealthOfPulse = new()
    {
        DamageDict = new Dictionary<string, FixedPoint2>
        {
            { "Blunt", -4 },
            { "Slash", -4 },
            { "Piercing", -4 },
            { "Heat", -4 },
            { "Cold", -4 },
            { "Shock", -4 },
        }
    };

    [ViewVariables(VVAccess.ReadOnly), DataField("flashDamage")]
    public DamageSpecifier FlashDamage = new()
    {
        DamageDict = new Dictionary<string, FixedPoint2>
        {
            { "Heat", 25 },
        }
    };
}

[Serializable]
public enum BlobTileType : byte
{
    Normal,
    Strong,
    Reflective,
    Resource,
    Storage,
    Turret,
    Node,
    Factory,
    Core,
    None,
}
