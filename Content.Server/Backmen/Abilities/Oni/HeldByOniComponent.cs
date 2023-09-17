namespace Content.Server.Backmen.Abilities.Oni;

[RegisterComponent]
public sealed partial class HeldByOniComponent : Component
{
    public EntityUid Holder = default!;
}
