using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Silicon.Components;

/// <summary>
///     Exists for use as a status effect. Adds a tv static shader to the client.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SeeingStaticComponent : Component
{
    /// <summary>
    ///     The multiplier applied to the effect.
    ///     Setting this to 0.5 will halve the effect throughout its entire duration, meaning it will never be fully opaque.
    ///     Setting this to 2 will double the effect throughout its entire duration, meaning it will be fully opaque for twice as long.
    /// </summary>
    [AutoNetworkedField]
    public float Multiplier = 1f;
}
