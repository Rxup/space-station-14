using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Flesh;

[NetworkedComponent()]
[Virtual]
public partial class SharedFleshHeartComponent : Component
{
    /// <summary>
    /// The visual state that is set when the emitter doesn't have enough power.
    /// </summary>
    [DataField("finalState")]
    public string? FinalState = "underpowered";
}
