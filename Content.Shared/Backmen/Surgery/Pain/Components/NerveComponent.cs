using System.Linq;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery.Pain.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NerveComponent : Component
{
    // Yuh-uh
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 PainMultiplier = 1.0f;

    // How feel able the pain is; The value can be decreased by pain suppressants and Nerve Damage.
    [ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 PainFeels => 1f + PainFeelingModifiers.Values.Sum(modifier => (float) modifier.Change);

    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<(EntityUid, string), PainFeelingModifier> PainFeelingModifiers = new();

    /// <summary>
    /// Nerve system, to which this nerve is parented.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public EntityUid ParentedNerveSystem;
}
