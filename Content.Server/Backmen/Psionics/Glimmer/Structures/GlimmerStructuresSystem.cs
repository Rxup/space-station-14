using Content.Server.Anomaly.Components;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Stunnable;
using Content.Server.Xenoarchaeology.XenoArtifacts.Events;
using Content.Shared.Anomaly.Components;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics.Components;
using Content.Shared.Backmen.Psionics.Glimmer;
using Content.Shared.Mobs.Systems;
using Content.Shared.Power;
using Content.Shared.StatusEffect;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Psionics.Glimmer;

/// <summary>
/// Handles structures which add/subtract glimmer.
/// </summary>
public sealed class GlimmerStructuresSystem : EntitySystem
{
    [Dependency] private readonly PowerReceiverSystem _powerReceiverSystem = default!;
    [Dependency] private readonly GlimmerSystem _glimmerSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly StunSystem _stunSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly PsionicsSystem _psionicsSystem = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffectsSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private EntityQuery<ApcPowerReceiverComponent> _apcPower;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnomalyVesselComponent, PowerChangedEvent>(OnAnomalyVesselPowerChanged);

        SubscribeLocalEvent<GlimmerSourceComponent, AnomalyPulseEvent>(OnAnomalyPulse);
        SubscribeLocalEvent<GlimmerSourceComponent, AnomalySupercriticalEvent>(OnAnomalySupercritical);
        SubscribeLocalEvent<GlimmerSourceComponent, ArtifactActivatedEvent>(OnArtifactActivated);

        _apcPower = GetEntityQuery<ApcPowerReceiverComponent>();
    }

    private void OnArtifactActivated(Entity<GlimmerSourceComponent> ent, ref ArtifactActivatedEvent args)
    {
        if (args.Activator != null &&
            !HasComp<PsionicInsulationComponent>(args.Activator) &&
            TryComp<PotentialPsionicComponent>(args.Activator, out var potentialPsionicComponent))
        {
            ZapTarget((args.Activator.Value, potentialPsionicComponent));
            return;
        }

        foreach (var target in _lookup.GetEntitiesInRange<PotentialPsionicComponent>(Transform(ent).Coordinates, ent.Comp.Range))
        {
            ZapTarget(target);
        }
    }

    private void ZapTarget(Entity<PotentialPsionicComponent> target)
    {
        if(HasComp<PsionicInsulationComponent>(target))
            return;

        _stunSystem.TryParalyze(target, TimeSpan.FromSeconds(5), false);
        _statusEffectsSystem.TryAddStatusEffect(target, "Stutter", TimeSpan.FromSeconds(10), false, "StutteringAccent");

        if (HasComp<PsionicComponent>(target))
        {
            _popupSystem.PopupEntity(Loc.GetString("noospheric-zap-seize"), target, target, Shared.Popups.PopupType.LargeCaution);
            _glimmerSystem.Glimmer += 25;
        }
        else
        {
            if (target.Comp.Rerolled)
            {
                target.Comp.Rerolled = false;
                _popupSystem.PopupEntity(Loc.GetString("noospheric-zap-seize-potential-regained"), target, target, Shared.Popups.PopupType.LargeCaution);
            }
            else
            {
                _psionicsSystem.RollPsionics(target, multiplier: 0.25f);
                _popupSystem.PopupEntity(Loc.GetString("noospheric-zap-seize"), target, target, Shared.Popups.PopupType.LargeCaution);
            }
        }
    }

    private void OnAnomalyVesselPowerChanged(EntityUid uid, AnomalyVesselComponent component, ref PowerChangedEvent args)
    {
        if (TryComp<GlimmerSourceComponent>(component.Anomaly, out var glimmerSource))
            glimmerSource.Active = args.Powered;
    }

    private void OnAnomalyPulse(EntityUid uid, GlimmerSourceComponent component, ref AnomalyPulseEvent args)
    {
        // Anomalies are meant to have GlimmerSource on them with the
        // active flag set to false, as they will be set to actively
        // generate glimmer when scanned to an anomaly vessel for
        // harvesting research points.
        //
        // It is not a bug that glimmer increases on pulse or
        // supercritical with an inactive glimmer source.
        //
        // However, this will need to be reworked if a distinction
        // needs to be made in the future. I suggest a GlimmerAnomaly
        // component.

        if (TryComp<AnomalyComponent>(args.Anomaly, out var anomaly))
            _glimmerSystem.Glimmer += (int) (5f * anomaly.Severity);
    }

    private void OnAnomalySupercritical(EntityUid uid, GlimmerSourceComponent component, ref AnomalySupercriticalEvent args)
    {
        _glimmerSystem.Glimmer += 400;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var q = EntityQueryEnumerator<GlimmerSourceComponent, MetaDataComponent>();
        while (q.MoveNext(out var owner, out var source, out var md))
        {
            if(Paused(owner, md))
                continue;

            if (!source.Active)
                continue;

            source.Accumulator += frameTime;

            if (source.Accumulator <= source.SecondsPerGlimmer)
                continue;

            if (_apcPower.TryComp(owner, out var powerReceiverComponent) && !_powerReceiverSystem.IsPowered(owner,powerReceiverComponent))
                continue;

            source.Accumulator -= source.SecondsPerGlimmer;

            if (source.AddToGlimmer)
            {
                _glimmerSystem.Glimmer++;
            }
            else
            {
                _glimmerSystem.Glimmer--;
            }
        }
    }
}
