using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Damage.Components;
using Robust.Shared.Utility;

namespace Content.Shared.Backmen.Damage;

/// <summary>
/// Ensures <see cref="InjurableComponent"/> is not combined with backmen damage models on one entity.
/// </summary>
public sealed partial class BackmenDamageModelExclusivitySystem : EntitySystem
{
    [Dependency] private EntityQuery<WoundableComponent> _woundableQuery = default!;
    [Dependency] private EntityQuery<ConsciousnessComponent> _consciousnessQuery = default!;
    [Dependency] private EntityQuery<BkmDetachedBodyComponent> _detachedBodyQuery = default!;

    public bool HasExclusiveBackmenDamageModel(EntityUid uid) =>
        _woundableQuery.HasComponent(uid)
        || _consciousnessQuery.HasComponent(uid)
        || _detachedBodyQuery.HasComponent(uid);

    /// <summary>
    /// Called from existing MapInit handlers — Robust allows only one subscription per (component, event).
    /// </summary>
    public void RemoveInjurableIfPresent(EntityUid uid)
    {
        if (!TryComp<InjurableComponent>(uid, out _))
            return;

        DebugTools.Assert(
            false,
            $"Entity {ToPrettyString(uid)} has Injurable together with a backmen damage model. " +
            "Remove Injurable from YAML; only one damage model is allowed.");

        RemComp<InjurableComponent>(uid);
    }
}

