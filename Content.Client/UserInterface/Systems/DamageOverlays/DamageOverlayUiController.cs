using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Player;

namespace Content.Client.UserInterface.Systems.DamageOverlays;

[UsedImplicitly]
public sealed class DamageOverlayUiController : UIController
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    [UISystemDependency] private readonly ConsciousnessSystem _consciousness = default!;
    [UISystemDependency] private readonly MobThresholdSystem _mobThresholdSystem = default!;
    private Overlays.DamageOverlay _overlay = default!;

    public override void Initialize()
    {
        _overlay = new Overlays.DamageOverlay();
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttach);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeNetworkEvent<MobThresholdChecked>(OnThresholdCheck);
    }

    private void OnPlayerAttach(LocalPlayerAttachedEvent args)
    {
        ClearOverlay();
        if (!EntityManager.TryGetComponent<MobStateComponent>(args.Entity, out var mobState))
            return;
        if (mobState.CurrentState != MobState.Dead)
            UpdateOverlays(args.Entity, mobState);
        _overlayManager.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        _overlayManager.RemoveOverlay(_overlay);
        ClearOverlay();
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.Target != _playerManager.LocalEntity)
            return;

        UpdateOverlays(args.Target, args.Component);
    }

    private void OnThresholdCheck(MobThresholdChecked args, EntitySessionEventArgs session)
    {
        if (!EntityManager.TryGetEntity(args.Uid, out var entity)
            || !_playerManager.LocalEntity.Equals(entity))
            return;

        UpdateOverlays(entity.Value);
    }

    private void ClearOverlay()
    {
        _overlay.DeadLevel = 0f;
        _overlay.CritLevel = 0f;
        _overlay.BruteLevel = 0f;
        _overlay.OxygenLevel = 0f;
    }

    // Jezi: adjust oxygen and hp overlays to use appropriate systems once bodysim is implemented
    // dw vro
    private void UpdateOverlays(EntityUid entity,
        MobStateComponent? mobState = null,
        BodyComponent? body = null,
        DamageableComponent? damageable = null,
        MobThresholdsComponent? thresholds = null)
    {
        if (mobState == null && !EntityManager.TryGetComponent(entity, out mobState) ||
            thresholds == null && !EntityManager.TryGetComponent(entity, out thresholds) ||
            body == null && !EntityManager.TryGetComponent(entity, out body) && damageable == null && !EntityManager.TryGetComponent(entity, out damageable))
            return;

        if (!thresholds.ShowOverlays)
        {
            ClearOverlay();
            return; //this entity intentionally has no overlays
        }

        _overlay.State = mobState.CurrentState;
        if (body == null && damageable != null)
        {
            if (!_mobThresholdSystem.TryGetIncapThreshold(entity, out var foundThreshold, thresholds))
                return; //this entity cannot die or crit!!

            var critThreshold = foundThreshold.Value;
            switch (mobState.CurrentState)
            {
                case MobState.Alive:
                {
                    if (damageable.DamagePerGroup.TryGetValue("Brute", out var bruteDamage))
                    {
                        _overlay.BruteLevel = FixedPoint2.Min(1f, bruteDamage / critThreshold).Float();
                    }

                    if (damageable.DamagePerGroup.TryGetValue("Airloss", out var oxyDamage))
                    {
                        _overlay.OxygenLevel = FixedPoint2.Min(1f, oxyDamage / critThreshold).Float();
                    }

                    if (_overlay.BruteLevel < 0.05f) // Don't show damage overlay if they're near enough to max.
                    {
                        _overlay.BruteLevel = 0;
                    }

                    _overlay.CritLevel = 0;
                    _overlay.DeadLevel = 0;
                    break;
                }
                case MobState.Critical:
                {
                    if (!_mobThresholdSystem.TryGetDeadPercentage(entity,
                            FixedPoint2.Max(0.0, damageable.TotalDamage), out var critLevel))
                        return;
                    _overlay.CritLevel = critLevel.Value.Float();

                    _overlay.BruteLevel = 0;
                    _overlay.DeadLevel = 0;
                    break;
                }
                case MobState.Dead:
                {
                    _overlay.BruteLevel = 0;
                    _overlay.CritLevel = 0;
                    break;
                }
            }
        }
        else if (body != null)
        {
            if (!EntityManager.TryGetComponent<ConsciousnessComponent>(entity, out var consciousness))
                return;

            switch (mobState.CurrentState)
            {
                case MobState.Alive:
                {
                    _overlay.CritLevel = 0;
                    _overlay.DeadLevel = 0;

                    if (consciousness.Consciousness <= 0 || consciousness.Consciousness >= consciousness.Cap)
                    {
                        _overlay.BruteLevel = 0;
                        return;
                    }

                    _overlay.BruteLevel = FixedPoint2.Min(1f,
                        (consciousness.Cap - consciousness.Consciousness) / (consciousness.Cap - consciousness.Threshold))
                        .Float();

                    if (_consciousness.TryGetNerveSystem(_playerManager.LocalEntity!.Value, out var nerveSys) &&
                        _consciousness.TryGetConsciousnessModifier(nerveSys.Value, nerveSys.Value, out var modifier, "Suffocation"))
                    {
                        _overlay.OxygenLevel = FixedPoint2.Min(1f, modifier.Value.Change / (consciousness.Cap - consciousness.Threshold)).Float();
                    }

                    if (_overlay.BruteLevel < 0.05f) // Don't show damage overlay if they're near enough to max.
                    {
                        _overlay.BruteLevel = 0;
                    }

                    break;
                }
                case MobState.Critical:
                {
                    _overlay.CritLevel = FixedPoint2.Min(1f,
                        (consciousness.Threshold - consciousness.Consciousness) / consciousness.Threshold)
                        .Float();

                    _overlay.BruteLevel = 0;
                    _overlay.DeadLevel = 0;
                    break;
                }
                case MobState.Dead:
                {
                    _overlay.BruteLevel = 0;
                    _overlay.CritLevel = 0;
                    break;
                }
            }
        }
    }
}
