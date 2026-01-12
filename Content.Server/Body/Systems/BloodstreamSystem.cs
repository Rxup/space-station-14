using Content.Server.Fluids.EntitySystems;
using Content.Server.Popups;
using Content.Shared.Alert;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Forensics;
using Content.Shared.Forensics.Components;
using Content.Shared.HealthExaminable;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Body.Systems;

public sealed class BloodstreamSystem : SharedBloodstreamSystem
{
    // backmen edit start
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly WoundSystem _wound = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;


    private float _bleedingSeverityTrade;
    private float _bleedsScalingTime;

    private EntityQuery<WoundableComponent> _woundableQuery;
    // backmen edit end

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodstreamComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<BloodstreamComponent, GenerateDnaEvent>(OnDnaGenerated);

        // backmen edit start
        SubscribeLocalEvent<BleedInflicterComponent, WoundHealAttemptEvent>(OnWoundHealAttempt);
        SubscribeLocalEvent<BleedInflicterComponent, WoundChangedEvent>(OnWoundChanged);

        Subs.CVar(_cfg, CCVars.BleedingSeverityTrade, value => _bleedingSeverityTrade = value, true);
        Subs.CVar(_cfg, CCVars.BleedsScalingTime, value => _bleedsScalingTime = value, true);

        BleedsQuery = GetEntityQuery<BleedInflicterComponent>();
        _woundableQuery = GetEntityQuery<WoundableComponent>();
        ConsciousnessQuery = GetEntityQuery<ConsciousnessComponent>();
        // backmen edit end
    }

    // not sure if we can move this to shared or not
    // it would certainly help if SolutionContainer was documented
    // but since we usually don't add the component dynamically to entities we can keep this unpredicted for now
    private void OnComponentInit(Entity<BloodstreamComponent> entity, ref ComponentInit args)
    {
        if (!SolutionContainer.EnsureSolution(entity.Owner,
                entity.Comp.BloodSolutionName,
                out var bloodSolution) ||
            !SolutionContainer.EnsureSolution(entity.Owner,
                entity.Comp.BloodTemporarySolutionName,
                out var tempSolution))
            return;

        bloodSolution.MaxVolume = entity.Comp.BloodReferenceSolution.Volume * entity.Comp.MaxVolumeModifier;
        tempSolution.MaxVolume = entity.Comp.BleedPuddleThreshold * 4; // give some leeway, for chemstream as well
        entity.Comp.BloodReferenceSolution.SetReagentData(GetEntityBloodData((entity, entity.Comp)));

        // Fill blood solution with BLOOD
        // The DNA string might not be initialized yet, but the reagent data gets updated in the GenerateDnaEvent subscription
        var solution = entity.Comp.BloodReferenceSolution.Clone();
        solution.ScaleTo(entity.Comp.BloodReferenceSolution.Volume - bloodSolution.Volume);
        bloodSolution.AddSolution(solution, PrototypeManager);
    }

    // forensics is not predicted yet
    private void OnDnaGenerated(Entity<BloodstreamComponent> entity, ref GenerateDnaEvent args)
    {
        if (SolutionContainer.ResolveSolution(entity.Owner, entity.Comp.BloodSolutionName, ref entity.Comp.BloodSolution, out var bloodSolution))
        {
            var data = NewEntityBloodData(entity);
            entity.Comp.BloodReferenceSolution.SetReagentData(data);

            foreach (var reagent in bloodSolution.Contents)
            {
                List<ReagentData> reagentData = reagent.Reagent.EnsureReagentData();
                reagentData.RemoveAll(x => x is DnaData);
                reagentData.AddRange(data);
            }
        }
        else
            Log.Error("Unable to set bloodstream DNA, solution entity could not be resolved");
    }

    // backmen edit start
    private void OnWoundHealAttempt(EntityUid uid, BleedInflicterComponent component, ref WoundHealAttemptEvent args)
    {
        if (component.IsBleeding)
            args.Cancelled = true;
    }

    private void OnWoundChanged(EntityUid uid, BleedInflicterComponent component, ref WoundChangedEvent args)
    {
        if (args.Component.WoundSeverityPoint < component.SeverityThreshold)
        {
            var woundable = args.Component.HoldingWoundable;
            if (!_woundableQuery.TryComp(woundable, out var woundableComp)
                || !TryComp(woundable, out BodyPartComponent? bodyPart) || !bodyPart.Body.HasValue)
                return;

            var bodyEnt = bodyPart.Body.Value;
            var bloodstream = Comp<BloodstreamComponent>(bodyEnt);

            if (args.Delta <= bloodstream.BloodHealedSoundThreshold
                     && component.IsBleeding && component.CauterizedBy.Contains(args.Component.DamageType))
            {
                foreach (var wound in
                         _wound.GetWoundableWoundsWithComp<BleedInflicterComponent>(woundable, woundableComp))
                {
                    var bleeds = wound.Comp2;
                    if (!bleeds.IsBleeding)
                        continue;

                    if (!bleeds.CauterizedBy.Contains(args.Component.DamageType))
                        continue;

                    bleeds.BleedingAmountRaw = 0;
                    bleeds.SeverityPenalty = 0;
                    bleeds.Scaling = 0;

                    bleeds.IsBleeding = false;
                }

                _audio.PlayPvs(bloodstream.BloodHealedSound, bodyEnt);
                _popupSystem.PopupEntity(Loc.GetString("bloodstream-component-wounds-cauterized"), bodyEnt, bodyEnt, PopupType.Medium);
            }
        }
        else
        {
            if (!CanWoundBleed((uid, component))
                && component.BleedingAmount < component.BleedingAmountRaw * component.ScalingLimit / 2)
            {
                component.BleedingAmountRaw = 0;
                component.SeverityPenalty = 0;
                component.Scaling = 0;

                component.IsBleeding = false;
            }
            else
            {
                if (args.Delta < 0)
                    return;

                // TODO: Instant bloodloss isn't funny at all
                //var prob = Math.Clamp((float) args.Delta / 25, 0, 1);
                //if (args.Delta > 0 && _robustRandom.Prob(prob))
                //{
                //    var woundable = args.Component.HoldingWoundable;
                //    if (TryComp(woundable, out BodyPartComponent? bodyPart) && bodyPart.Body.HasValue)
                //    {
                //        var bodyEnt = bodyPart.Body.Value;
                //        var bloodstream = Comp<BloodstreamComponent>(bodyEnt);

                        // instant blood loss
                //        TryModifyBloodLevel(bodyEnt, (-args.Delta) / 15, bloodstream);
                //        _audio.PlayPvs(bloodstream.InstantBloodSound, bodyEnt);
                //    }
                //}

                var oldBleedsAmount = component.BleedingAmountRaw;
                component.BleedingAmountRaw = args.Component.WoundSeverityPoint * _bleedingSeverityTrade;

                var severityPenalty = component.BleedingAmountRaw - oldBleedsAmount / _bleedsScalingTime;
                component.SeverityPenalty += severityPenalty;

                if (component.IsBleeding)
                {
                    // Pump up the bleeding if hit again.
                    component.ScalingLimit += args.Delta * _bleedingSeverityTrade;
                }

                var formula = (float) (args.Component.WoundSeverityPoint / _bleedsScalingTime * component.ScalingSpeed);
                component.ScalingFinishesAt = _gameTiming.CurTime + TimeSpan.FromSeconds(formula);
                component.ScalingStartsAt = _gameTiming.CurTime;

                // wounds that BLEED will not HEAL.
                component.IsBleeding = true;
            }
        }

        Dirty(uid, component);
    }

    /// <summary>
    /// Add a bleed-ability modifier on woundable
    /// </summary>
    /// <param name="woundable">Entity uid of the woundable to apply the modifiers</param>
    /// <param name="identifier">string identifier of the modifier</param>
    /// <param name="priority">Priority of the said modifier</param>
    /// <param name="canBleed">Should the wounds bleed?</param>
    /// <param name="force">If forced, won't stop after failing to apply one modifier</param>
    /// <param name="woundableComp">Woundable Component</param>
    /// <returns>Return true if applied</returns>
    [PublicAPI]
    public bool TryAddBleedModifier(
        EntityUid woundable,
        string identifier,
        int priority,
        bool canBleed,
        bool force = false,
        WoundableComponent? woundableComp = null)
    {
        if (!_woundableQuery.Resolve(woundable, ref woundableComp))
            return false;

        foreach (var woundEnt in _wound.GetWoundableWoundsWithComp<BleedInflicterComponent>(woundable, woundableComp))
        {
            if (TryAddBleedModifier(woundEnt, identifier, priority, canBleed, woundEnt.Comp2))
                continue;

            if (!force)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Add a bleed-ability modifier
    /// </summary>
    /// <param name="uid">Entity uid of the wound</param>
    /// <param name="identifier">string identifier of the modifier</param>
    /// <param name="priority">Priority of the said modifier</param>
    /// <param name="canBleed">Should the wound bleed?</param>
    /// <param name="comp">Bleed Inflicter Component</param>
    /// <returns>Return true if applied</returns>
    [PublicAPI]
    public bool TryAddBleedModifier(
        EntityUid uid,
        string identifier,
        int priority,
        bool canBleed,
        BleedInflicterComponent? comp = null)
    {
        if (!BleedsQuery.Resolve(uid, ref comp))
            return false;

        if (!comp.BleedingModifiers.TryAdd(identifier, (priority, canBleed)))
            return false;

        Dirty(uid, comp);
        return true;
    }

    /// <summary>
    /// Remove a bleed-ability modifier from a woundable
    /// </summary>
    /// <param name="uid">Entity uid of the woundable</param>
    /// <param name="identifier">string identifier of the modifier</param>
    /// <param name="force">If forced, won't stop applying modifiers after failing one wound</param>
    /// <param name="woundable">Woundable Component</param>
    /// <returns>Returns true if removed all modifiers ON WOUNDABLE</returns>
    [PublicAPI]
    public bool TryRemoveBleedModifier(
        EntityUid uid,
        string identifier,
        bool force = false,
        WoundableComponent? woundable = null)
    {
        if (!_woundableQuery.Resolve(uid, ref woundable))
            return false;

        foreach (var woundEnt in _wound.GetWoundableWoundsWithComp<BleedInflicterComponent>(uid, woundable))
        {
            if (TryRemoveBleedModifier(woundEnt, identifier, woundEnt.Comp2))
                continue;

            if (!force)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Remove a bleed-ability modifier
    /// </summary>
    /// <param name="uid">Entity uid of the wound</param>
    /// <param name="identifier">string identifier of the modifier</param>
    /// <param name="comp">Bleed Inflicter Component</param>
    /// <returns>Return true if removed</returns>
    public bool TryRemoveBleedModifier(
        EntityUid uid,
        string identifier,
        BleedInflicterComponent? comp = null)
    {
        if (!BleedsQuery.Resolve(uid, ref comp))
            return false;

        if (!comp.BleedingModifiers.Remove(identifier))
            return false;

        Dirty(uid, comp);
        return true;
    }

    /// <summary>
    /// Redact a modifiers meta data
    /// </summary>
    /// <param name="wound">The wound entity uid</param>
    /// <param name="identifier">Identifier of the modifier</param>
    /// <param name="priority">Priority to set</param>
    /// <param name="canBleed">Should it bleed?</param>
    /// <param name="bleeds">Bleed Inflicter Component</param>
    /// <returns>true if was changed</returns>
    [PublicAPI]
    public bool ChangeBleedsModifierMetadata(
        EntityUid wound,
        string identifier,
        bool canBleed,
        int? priority,
        BleedInflicterComponent? bleeds = null)
    {
        if (!BleedsQuery.Resolve(wound, ref bleeds))
            return false;

        if (!bleeds.BleedingModifiers.TryGetValue(identifier, out var pair))
            return false;

        bleeds.BleedingModifiers[identifier] = (Priority: priority ?? pair.Priority, CanBleed: canBleed);
        return true;
    }

    /// <summary>
    /// Redact a modifiers meta data
    /// </summary>
    /// <param name="wound">The wound entity uid</param>
    /// <param name="identifier">Identifier of the modifier</param>
    /// <param name="priority">Priority to set</param>
    /// <param name="canBleed">Should it bleed?</param>
    /// <param name="bleeds">Bleed Inflicter Component</param>
    /// <returns>true if was changed</returns>
    [PublicAPI]
    public bool ChangeBleedsModifierMetadata(
        EntityUid wound,
        string identifier,
        int priority,
        bool? canBleed,
        BleedInflicterComponent? bleeds = null)
    {
        if (!BleedsQuery.Resolve(wound, ref bleeds))
            return false;

        if (!bleeds.BleedingModifiers.TryGetValue(identifier, out var pair))
            return false;

        bleeds.BleedingModifiers[identifier] = (Priority: priority, CanBleed: canBleed ?? pair.CanBleed);
        return true;
    }
    // backmen edit end
}
