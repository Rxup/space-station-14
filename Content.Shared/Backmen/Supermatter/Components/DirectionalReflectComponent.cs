using Content.Shared.Weapons.Reflect;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Supermatter.Components;

/// <summary>
/// Redirects projectiles out through the facing side.
/// Shots from the back or sides are turned toward the output face; only direct hits into the output face are absorbed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DirectionalReflectComponent : Component
{
    /// <summary>
    /// Projectile reflective types that this reflector accepts.
    /// </summary>
    [DataField]
    public ReflectType Reflects = ReflectType.Energy;

    /// <summary>
    /// Angular half-width of the output face that absorbs shots instead of redirecting them.
    /// </summary>
    [DataField]
    public Angle FrontAbsorbAngle = Angle.FromDegrees(45);

    /// <summary>
    /// Sound played when a projectile is redirected.
    /// </summary>
    [DataField]
    public SoundSpecifier? SoundOnReflect = new SoundPathSpecifier(
        "/Audio/Weapons/Guns/Hits/laser_sear_wall.ogg",
        AudioParams.Default.WithVariation(0.05f));
}
