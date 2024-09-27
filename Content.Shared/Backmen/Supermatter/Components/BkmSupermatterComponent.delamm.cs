namespace Content.Shared.Backmen.Supermatter.Components;

public partial class BkmSupermatterComponent
{
    /// <summary>
    /// The point at which we delamm
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    [DataField("explosionPoint")]
    public int ExplosionPoint = 900;

    [ViewVariables(VVAccess.ReadOnly)]
    public DelamType PreferredDelamType = DelamType.Explosion;

    //Are we delamming?
    [ViewVariables(VVAccess.ReadOnly)]
    public bool Delamming = false;

    //Explosion totalIntensity value
    [ViewVariables(VVAccess.ReadOnly)]
    [DataField("totalIntensity")]
    public float TotalIntensity= 500000f;

    //Explosion radius value
    [ViewVariables(VVAccess.ReadOnly)]
    [DataField("radius")]
    public float Radius = 500f;

    /// <summary>
    /// These would be what you would get at point blank, decreases with distance
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [DataField("detonationRads")]
    public float DetonationRads = 200f;
}
