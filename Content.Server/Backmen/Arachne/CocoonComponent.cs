namespace Content.Server.Backmen.Arachne;

[RegisterComponent]
public sealed partial class CocoonComponent : Component
{
    public bool WasReplacementAccent = false;

    public string OldAccent = "";

    [DataField("damagePassthrough")]
    public float DamagePassthrough = 0.5f;
}
