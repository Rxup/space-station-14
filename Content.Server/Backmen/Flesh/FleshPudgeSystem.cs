using Content.Server.Actions;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.Popups;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Backmen.Flesh;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Collections;

namespace Content.Server.Backmen.Flesh;

public sealed class FleshPudgeSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ActionsSystem _action = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionSystem = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GunSystem _gunSystem = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FleshPudgeComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FleshPudgeComponent, FleshPudgeThrowWormActionEvent>(OnThrowWorm);
        SubscribeLocalEvent<FleshPudgeComponent, FleshPudgeAbsorbBloodPoolActionEvent>(OnAbsorbBloodPoolActionEvent);
        SubscribeLocalEvent<FleshPudgeComponent, FleshPudgeAcidSpitActionEvent>(OnAcidSpit);
    }
    private void OnThrowWorm(EntityUid uid, FleshPudgeComponent component, FleshPudgeThrowWormActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var xform = Transform(uid);
        var worm = Spawn(component.WormMobSpawnId, xform.Coordinates);

        var mapCoords = _transform.ToMapCoordinates(args.Target);
        var direction = mapCoords.Position - _transform.GetMapCoordinates(xform).Position;

        _throwing.TryThrow(worm, direction, 7F, uid, 10F);
        if (component.SoundThrowWorm != null)
        {
            _audioSystem.PlayPvs(component.SoundThrowWorm, uid, component.SoundThrowWorm.Params);
        }
        _popup.PopupEntity(Loc.GetString("flesh-pudge-throw-worm-popup"), uid, PopupType.LargeCaution);
    }

    private void OnAcidSpit(EntityUid uid, FleshPudgeComponent component, FleshPudgeAcidSpitActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var xform = Transform(uid);
        var acidBullet = Spawn(component.BulletAcidSpawnId, xform.Coordinates);
        var mapCoords = _transform.ToMapCoordinates(args.Target);
        var direction = mapCoords.Position -  _transform.GetMapCoordinates(xform).Position;
        var userVelocity = _physics.GetMapLinearVelocity(uid);

        _gunSystem.ShootProjectile(acidBullet, direction, userVelocity, uid, uid);
        _audioSystem.PlayPvs(component.BloodAbsorbSound, uid, component.BloodAbsorbSound.Params);
    }

    private void OnAbsorbBloodPoolActionEvent(EntityUid uid, FleshPudgeComponent component,
        FleshPudgeAbsorbBloodPoolActionEvent args)
    {
        if (args.Handled)
            return;

        var xform = Transform(uid);
        var puddles = new ValueList<(EntityUid Entity, string Solution)>();
        puddles.Clear();
        foreach (var entity in _lookup.GetEntitiesInRange<PuddleComponent>(xform.Coordinates, 0.5f))
        {
            puddles.Add((entity, entity.Comp.SolutionName));
        }

        if (puddles.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("flesh-cultist-not-find-puddles"),
                uid, uid, PopupType.Large);
            return;
        }

        var totalBloodQuantity = 0f;

        foreach (var (puddle, solution) in puddles)
        {
            if (!_solutionSystem.TryGetSolution(puddle, solution, out _, out var puddleSolution))
            {
                continue;
            }
            var hasImpurities = false;
            FixedPoint2 puddleBloodQuantity = 0;
            foreach (var puddleSolutionContent in puddleSolution.Contents.ToArray())
            {
                if (puddleSolutionContent.Reagent.Prototype != "Blood")
                {
                    hasImpurities = true;
                }
                else
                {
                    puddleBloodQuantity += puddleSolutionContent.Quantity;
                }
            }
            if (hasImpurities)
                continue;
            totalBloodQuantity += puddleBloodQuantity.Float();
            QueueDel(puddle);
        }

        if (totalBloodQuantity == 0)
        {
            _popup.PopupEntity(Loc.GetString("flesh-cultist-cant-absorb-puddle"),
                uid, uid, PopupType.Large);
            return;
        }

        _audioSystem.PlayPvs(component.BloodAbsorbSound, uid, component.BloodAbsorbSound.Params);
        _popup.PopupEntity(Loc.GetString("flesh-cultist-absorb-puddle", ("Entity", uid)),
            uid, uid, PopupType.Large);

        var transferSolution = new Solution();
        foreach (var reagent in component.HealBloodAbsorbReagents.ToArray())
        {
            transferSolution.AddReagent(reagent.Reagent, reagent.Quantity * (totalBloodQuantity / 10));
        }
        if (_solutionSystem.TryGetInjectableSolution(uid, out var injectableSolution, out _))
        {
            _solutionSystem.TryAddSolution(injectableSolution.Value, transferSolution);
        }
        args.Handled = true;
    }

    private void OnStartup(EntityUid uid, FleshPudgeComponent component, ComponentStartup args)
    {
        _action.AddAction(uid, ref component.AcidSpitAction, component.ActionAcidSpit);
        _action.AddAction(uid, ref component.ThrowWormAction, component.ActionThrowWorm);
        _action.AddAction(uid, ref component.AbsorbBloodPoolAction, component.ActionAbsorbBloodPool);
    }
}
