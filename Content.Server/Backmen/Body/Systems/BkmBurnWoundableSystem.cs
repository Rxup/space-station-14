using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;

namespace Content.Server.Backmen.Body.Systems;

/// <summary>
/// Determines when heat damage should vaporize a woundable into ash.
/// </summary>
public sealed partial class BkmBurnWoundableSystem : EntitySystem
{
    [Dependency] private WoundSystem _wounds = default!;

    public bool ShouldBurnToAsh(EntityUid woundable, WoundableComponent? component = null) =>
        _wounds.ShouldBurnToAshNullable((woundable, component));
}
