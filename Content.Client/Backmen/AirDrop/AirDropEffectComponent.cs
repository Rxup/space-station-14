using Robust.Shared.GameObjects;

namespace Content.Client.Backmen.AirDrop;

/// <summary>
/// Client-side effect spawned for an <see cref="Content.Shared.Backmen.AirDrop.AirDropComponent"/> drop sequence.
/// </summary>
[RegisterComponent]
public sealed partial class AirDropEffectComponent : Component
{
    public EntityUid Controller;

    public AirDropEffectKind Kind;
}

public enum AirDropEffectKind : byte
{
    Target,
    Falling,
}
