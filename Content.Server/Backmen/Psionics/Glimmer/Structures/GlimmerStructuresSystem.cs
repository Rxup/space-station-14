using System.Linq;
using Content.Server.Anomaly.Components;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.Stunnable;
using Content.Shared.Anomaly.Components;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics.Components;
using Content.Shared.Backmen.Psionics.Glimmer;
using Content.Shared.Power;
using Content.Shared.Station.Components;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Psionics.Glimmer;

/// <summary>
/// Handles structures which add/subtract glimmer.
/// </summary>
public sealed partial class GlimmerStructuresSystem : EntitySystem
{
    [Dependency] private PowerReceiverSystem _powerReceiverSystem = default!;
    [Dependency] private GlimmerSystem _glimmerSystem = default!;
    [Dependency] private StunSystem _stunSystem = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private PsionicsSystem _psionicsSystem = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private Shared.StatusEffectNew.StatusEffectsSystem _statusEffects = default!;


    [Dependency] private EntityQuery<ApcPowerReceiverComponent> _apcPower;
    [Dependency] private EntityQuery<XenoArtifactComponent> _artQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnomalyVesselComponent, PowerChangedEvent>(OnAnomalyVesselPowerChanged);

        SubscribeLocalEvent<GlimmerSourceComponent, AnomalyPulseEvent>(OnAnomalyPulse);
        SubscribeLocalEvent<GlimmerSourceComponent, AnomalySupercriticalEvent>(OnAnomalySupercritical);
        SubscribeLocalEvent<GlimmerSourceComponent, XenoArtifactActivatedEvent>(OnArtifactActivated);
    }

    private void OnArtifactActivated(Entity<GlimmerSourceComponent> ent, ref XenoArtifactActivatedEvent args)
    {
        if (args.User != null &&
            !_statusEffects.HasEffectComp<PsionicInsulationComponent>(args.User) &&
            TryComp<PotentialPsionicComponent>(args.User, out var potentialPsionicComponent))
        {
            ZapTarget((args.User.Value, potentialPsionicComponent));
            return;
        }

        foreach (var target in _lookup.GetEntitiesInRange<PotentialPsionicComponent>(Transform(ent).Coordinates, ent.Comp.Range))
        {
            ZapTarget(target);
        }
    }

    private static readonly EntProtoId StatusEffectStutter = "StatusEffectStutter";

    private void ZapTarget(Entity<PotentialPsionicComponent> target)
    {
        if(_statusEffects.HasEffectComp<PsionicInsulationComponent>(target))
            return;

        _stunSystem.TryUpdateParalyzeDuration(target, TimeSpan.FromSeconds(5));
        _statusEffects.TryAddStatusEffectDuration(target, StatusEffectStutter, TimeSpan.FromSeconds(10));

        if (HasComp<PsionicComponent>(target))
        {
            _popupSystem.PopupEntity(Loc.GetString("noospheric-zap-seize"), target, Shared.Popups.PopupType.LargeCaution);
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

        var stationGrids = _stationSystem.GetStations()
            .Where(x => x.Valid)
            .Where(HasComp<StationEventEligibleComponent>)
            .SelectMany(x => Comp<StationDataComponent>(x).Grids)
            .ToArray();

        var q = EntityQueryEnumerator<GlimmerSourceComponent, MetaDataComponent, TransformComponent>();
        while (q.MoveNext(out var owner, out var source, out var md, out var xform))
        {
            if(Paused(owner, md))
                continue;

            if (!source.Active)
                continue;

            if(xform.GridUid == null || !stationGrids.Contains(xform.GridUid.Value))
                continue;

            source.Accumulator += frameTime;

            if (source.Accumulator <= source.SecondsPerGlimmer)
                continue;

            source.Accumulator -= source.SecondsPerGlimmer;

            if (_artQuery.TryComp(owner, out var artifactComponent) && artifactComponent.Suppressed)
            {
                // art is IsSuppressed = true, so skip!
                continue;
            }

            if (_apcPower.TryComp(owner, out var powerReceiverComponent) && !_powerReceiverSystem.IsPowered(owner,powerReceiverComponent))
                continue;

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
