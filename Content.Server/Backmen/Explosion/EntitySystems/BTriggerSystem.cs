using Content.Server.Backmen.Fluids.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Effects;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Trigger;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Explosion.EntitySystems;

public sealed partial class BTriggerSystem : EntitySystem
{
    private const float SplashRange = 1.0f;

    [Dependency] private PuddleSystem _puddleSystem = default!;
    [Dependency] private ReactiveSystem _reactive = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedColorFlashEffectSystem _color = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedPopupSystem _popups = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SplashOnTriggerComponent, TriggerEvent>(OnSplashTrigger);
    }

    private void OnSplashTrigger(EntityUid uid, SplashOnTriggerComponent component, TriggerEvent args)
    {
        // Prefer the collided entity's position so AoE reliably covers the hit target.
        var coords = args.User is { } target && !TerminatingOrDeleted(target)
            ? Transform(target).Coordinates
            : Transform(uid).Coordinates;

        if (!coords.IsValid(EntityManager))
            return;

        var transferSolution = new Solution();
        foreach (var reagent in component.SplashReagents)
        {
            transferSolution.AddReagent(reagent.Reagent, reagent.Quantity);
        }

        if (transferSolution.Volume == 0)
            return;

        // Don't splash the caster (projectile shooter).
        EntityUid? exclude = null;
        if (TryComp<ProjectileComponent>(uid, out var projectile))
            exclude = projectile.Shooter;

        SplashAt(uid, coords, transferSolution, exclude);
    }

    /// <summary>
    /// AoE Touch splash like <see cref="PuddleSystem.TrySplashSpillAt"/>, but skips <paramref name="exclude"/>.
    /// </summary>
    private void SplashAt(EntityUid source, EntityCoordinates coordinates, Solution solution, EntityUid? exclude)
    {
        var spilled = solution.SplitSolution(solution.Volume);
        var targets = new List<EntityUid>();
        var reactive = new HashSet<Entity<ReactiveComponent>>();
        _lookup.GetEntitiesInRange(coordinates, SplashRange, reactive);

        foreach (var ent in reactive)
        {
            var owner = ent.Owner;
            if (exclude != null && owner == exclude.Value)
                continue;

            // between 5 and 30%
            var splitAmount = spilled.Volume * _random.NextFloat(0.05f, 0.30f);
            var splitSolution = spilled.SplitSolution(splitAmount);

            targets.Add(owner);
            _reactive.DoEntityReaction(owner, splitSolution, ReactionMethod.Touch);
            _popups.PopupEntity(Loc.GetString("spill-land-spilled-on-other",
                    ("spillable", source),
                    ("target", Identity.Entity(owner, EntityManager))),
                owner,
                PopupType.SmallCaution);
        }

        if (targets.Count > 0)
        {
            _color.RaiseEffect(spilled.GetColor(_prototype), targets,
                Filter.Pvs(source, entityManager: EntityManager));
        }

        _puddleSystem.TrySpillAt(coordinates, spilled, out _);
    }
}
