using Content.Server.Atmos.EntitySystems;
using Content.Server.Backmen.Body.Systems;
using Content.Server.Backmen.Body;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Body;
using Content.Shared.Inventory;
using Robust.Server.GameObjects;

namespace Content.Server.Backmen.Body.Systems;

/// <summary>
/// Full-body and supermatter burn handling for layered humanoids vs flat NPCs.
/// </summary>
public sealed class BkmBurnBodySystem : EntitySystem
{
    [Dependency] private readonly BkmBodySystem _body = default!;
    [Dependency] private readonly BkmBodySharedSystem _bodyShared = default!;
    [Dependency] private readonly BkmBrainPreservationSystem _brain = default!;
    [Dependency] private readonly BkmBurnEffectsSystem _effects = default!;
    [Dependency] private readonly BkmDetachedBodyBurnSystem _detachedBurn = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    /// <summary>
    /// Handles supermatter and other instant-dust paths. Returns true when custom handling ran.
    /// </summary>
    public bool TryDustEntity(EntityUid target, EntityUid cause, bool playSound = true)
    {
        if (_brain.TryPreserveBrain(target, _transform.GetMapCoordinates(target), cause))
            return true;

        if (TryComp<BodyComponent>(target, out _) && !_bodyShared.UsesFlatOrgans(target))
        {
            BurnMobToAsh(target, cause, playSound);
            return true;
        }

        if (TryComp<BkmDetachedBodyComponent>(target, out var detached))
        {
            if (detached.RootOrgan is { } root && !TerminatingOrDeleted(root))
            {
                _detachedBurn.BurnDetachedBundleRoot((target, detached), root, cause);
                return true;
            }

            if (playSound)
                _effects.PlayBurnSound(cause);

            _effects.SpawnAshAt(_transform.GetMapCoordinates(target));
            QueueDel(target);
            return true;
        }

        return false;
    }

    public void BurnMobToAsh(EntityUid body, EntityUid? cause = null, bool playSound = true)
    {
        if (TryComp<InventoryComponent>(body, out var inventory))
        {
            foreach (var item in _inventory.GetHandOrInventoryEntities(body))
                _transform.DropNextTo(item, body);
        }

        _brain.PreserveBrainsOnBody(body, cause);

        if (playSound)
            _effects.PlayBurnSound(body);

        _effects.PopupBodyBurn(body);
        _effects.SpawnAshAt(_transform.GetMapCoordinates(body));

        if (HasComp<BodyComponent>(body) && !_bodyShared.UsesFlatOrgans(body))
        {
            var gibs = _body.GibBody(body, gibOrgans: true, launchGibs: true, splatModifier: 2f);

            foreach (var gib in gibs)
            {
                if (_brain.TryPreserveBrain(gib, _transform.GetMapCoordinates(gib), cause))
                    continue;

                if (!HasComp<BkmDetachedBodyComponent>(gib))
                    continue;

                _flammable.Ignite(gib, cause ?? body);
            }
        }

        QueueDel(body);
    }

    public void DustFlatEntity(EntityUid target, EntityUid cause, bool playSound = true)
    {
        if (playSound)
            _effects.PlayBurnSound(cause);

        _effects.SpawnAshAt(_transform.GetMapCoordinates(target));
        QueueDel(target);
    }
}
