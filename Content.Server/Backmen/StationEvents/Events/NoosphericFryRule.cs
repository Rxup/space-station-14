using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Backmen.Psionics.Glimmer;
using Content.Server.Backmen.StationEvents.Components;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.StationEvents.Events;
using Content.Shared.Atmos.Components;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics.Components;
using Content.Shared.Backmen.Psionics.Glimmer;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;

namespace Content.Server.Backmen.StationEvents.Events;

/// <summary>
/// Fries tinfoil hats and cages
/// </summary>
internal sealed partial class NoosphericFryRule : StationEventSystem<NoosphericFryRuleComponent>
{
    [Dependency] private MobStateSystem _mobStateSystem = default!;
    [Dependency] private InventorySystem _inventorySystem = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private GlimmerSystem _glimmerSystem = default!;
    [Dependency] private FlammableSystem _flammableSystem = default!;
    [Dependency] private GlimmerReactiveSystem _glimmerReactiveSystem = default!;
    [Dependency] private AnchorableSystem _anchorableSystem = default!;
    [Dependency] private PowerReceiverSystem _powerReceiverSystem = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private MapSystem _mapSystem = default!;
    [Dependency] private Shared.StatusEffectNew.StatusEffectsSystem _statusEffects = default!;

    private static readonly SoundSpecifier BurnSound = new SoundPathSpecifier("/Audio/Effects/lightburn.ogg");

    protected override void Started(EntityUid uid, NoosphericFryRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        List<(EntityUid wearer, Entity<TinfoilHatComponent> worn)> psionicList = new();

        var query = EntityQueryEnumerator<PotentialPsionicComponent, MobStateComponent>();
        while (query.MoveNext(out var psion, out _, out var mobState))
        {
            if(!_statusEffects.HasEffectComp<PsionicInsulationComponent>(psion))
                continue;

            if (_mobStateSystem.IsIncapacitated(psion,mobState))
                continue;

            if (!_inventorySystem.TryGetSlotEntity(psion, "head", out var headItem))
                continue;

            if (!TryComp<TinfoilHatComponent>(headItem, out var tinfoil))
                continue;

            psionicList.Add((psion, (headItem.Value, tinfoil)));
        }

        foreach (var (wearer,(wornOwner,worn)) in psionicList)
        {
            if (worn.DestroyOnFry)
            {
                QueueDel(wornOwner);
                Spawn("Ash", Transform(wearer).Coordinates);
                _popupSystem.PopupEntity(Loc.GetString("psionic-burns-up", ("item", wornOwner)), wearer, Filter.Pvs(wearer), true, Shared.Popups.PopupType.MediumCaution);
                _audioSystem.PlayPvs(BurnSound, wearer);
            }
            else
            {
                _popupSystem.PopupEntity(Loc.GetString("psionic-burn-resist", ("item", wornOwner)), wearer, Filter.Pvs(wearer), true, Shared.Popups.PopupType.SmallCaution);
                _audioSystem.PlayPvs(BurnSound, wearer);
            }

            DamageSpecifier damage = new();
            damage.DamageDict.Add("Heat", 2.5);
            damage.DamageDict.Add("Shock", 2.5);

            if (_glimmerSystem.Glimmer is > 500 and < 750)
            {
                damage *= 2;
                if (TryComp<FlammableComponent>(wearer, out var flammableComponent))
                {
                    flammableComponent.FireStacks += 1;
                    _flammableSystem.Ignite(wearer,uid, flammableComponent);
                }
            }
            else if (_glimmerSystem.Glimmer > 750)
            {
                damage *= 3;
                if (TryComp<FlammableComponent>(wearer, out var flammableComponent))
                {
                    flammableComponent.FireStacks += 2;
                    _flammableSystem.Ignite(wearer, uid,flammableComponent);
                }
            }

            _damageableSystem.TryChangeDamage(wearer, damage, true, true);
        }

        // for probers:
        var queryReactive = EntityQueryEnumerator<GlimmerReactiveComponent, TransformComponent, PhysicsComponent>();
        while (queryReactive.MoveNext(out var reactive, out _, out var xform, out var physics))
        {
            // shoot out three bolts of lighting...
            _glimmerReactiveSystem.BeamRandomNearProber(reactive, 3, 12);

            // try to anchor if we can
            if (!xform.Anchored)
            {
                var coordinates = xform.Coordinates;
                var gridUid = xform.GridUid;
                if (!TryComp<MapGridComponent>(gridUid, out var grid))
                    continue;

                var tileIndices = _mapSystem.TileIndicesFor(gridUid.Value, grid, coordinates);

                if (_anchorableSystem.TileFree((gridUid.Value, grid), tileIndices, physics.CollisionLayer, physics.CollisionMask))
                    _transformSystem.AnchorEntity(reactive, xform);
            }

            if (!TryComp<ApcPowerReceiverComponent>(reactive, out var power))
                continue;

            // If it's been turned off, turn it back on.
            if (power.PowerDisabled)
                _powerReceiverSystem.TogglePower(reactive, false);
        }
    }
}
