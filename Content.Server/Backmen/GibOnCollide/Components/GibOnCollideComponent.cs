using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Backmen.GibOnCollide;

/// <summary>
///  Gibs entity on collide.
/// </summary>
[RegisterComponent]
public sealed partial class GibOnCollideComponent : Component
{
    [DataField("gibSound")]
    public SoundSpecifier GibSound = new SoundPathSpecifier("/Audio/Effects/adminhelp.ogg");

    /// <summary>
    ///  Cooldown time.
    /// </summary>
    [DataField("gibCooldown", customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan GibCooldown = TimeSpan.FromSeconds(0);

    /// <summary>
    ///  Message when gibbed entity.
    /// </summary>
    [DataField("gibMessage")]
    public string? GibMessage = "UNROBUST!";

    /// <summary>
    ///  If true, only alive entity will be gibbed. If false, everyone will be gibbed.
    /// </summary>
    [DataField("gibOnlyAlive")]
    public bool GibOnlyAlive = true;

    public TimeSpan LastGibTime;
}
