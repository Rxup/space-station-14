using Robust.Shared.GameObjects;

namespace Content.Server._White.Immersive.Components;

[RegisterComponent]
public sealed partial class ImmersiveComponent : Component
{
    [DataField]
    public float EyeModifier = 0.9f;

    [DataField]
    public float TelescopeDivisor = 0.25f;

    [DataField]
    public float TelescopeLerpAmount = 0.15f;
}
