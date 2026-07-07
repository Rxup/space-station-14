using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Healing;

namespace Content.Shared.Backmen.Medical;

/// <summary>
/// Shared woundable targeting for topical medical items. No event subscriptions.
/// </summary>
public sealed partial class BackmenMedicalTargetSystem : EntitySystem
{
    [Dependency] private BkmBodySharedSystem _body = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private SharedBloodstreamSystem _bloodstream = default!;

    public FixedPoint2 GetTotalBleeds(EntityUid woundable, WoundableComponent? woundableComp = null)
    {
        if (!Resolve(woundable, ref woundableComp, false))
            return FixedPoint2.Zero;

        return _wounds.GetWoundableWoundsWithComp<BleedInflicterComponent>(woundable, woundableComp)
            .Where(wound => wound.Comp2.BleedingAmount > 0 && _bloodstream.CanWoundBleed((wound, wound.Comp2))) // backmen: tourniquet-bleeding-display
            .Aggregate(FixedPoint2.Zero, (current, wound) => current + wound.Comp2.BleedingAmountRaw);
    }

    // start-backmen: medical-targeting
    public bool TryResolveHealTarget(
        EntityUid body,
        EntityUid healer,
        HealingComponent healing,
        [NotNullWhen(true)] out EntityUid woundable,
        out Dictionary<string, FixedPoint2> stuffToHeal,
        out bool usedFallback)
    {
        woundable = EntityUid.Invalid;
        stuffToHeal = new Dictionary<string, FixedPoint2>();
        usedFallback = false;

        if (!TryComp<TargetingComponent>(healer, out var targeting))
            return false;

        bool TryCandidate(EntityUid candidate, out Dictionary<string, FixedPoint2> healDict, out FixedPoint2 score)
        {
            healDict = new Dictionary<string, FixedPoint2>();
            score = FixedPoint2.Zero;

            if (!TryComp<WoundableComponent>(candidate, out var woundableComp))
                return false;

            healDict = healing.Damage.DamageDict
                .Where(damage => _wounds.HasDamageOfType(candidate, damage.Key))
                .ToDictionary(damage => damage.Key.Id, damage => damage.Value);

            var bleeds = GetTotalBleeds(candidate, woundableComp);
            if (bleeds > healing.UnableToHealBleedsThreshold
                && (healing.BloodlossModifier != 0 || healDict.Count == 0))
                return false;

            foreach (var amount in healDict.Values)
                score += amount;

            if (healing.BloodlossModifier != 0)
                score += bleeds;

            if (healing.ModifyBloodLevel > 0)
                score += FixedPoint2.New(0.01);

            return score > 0;
        }

        var (partType, symmetry) = _body.ConvertTargetBodyPart(targeting.Target);
        var selected = EntityUid.Invalid;
        if (_body.TryGetWoundableTargetByType(body, partType, symmetry, out selected)
            && TryCandidate(selected, out stuffToHeal, out _))
        {
            woundable = selected;
            return true;
        }

        EntityUid? best = null;
        var bestScore = FixedPoint2.Zero;
        Dictionary<string, FixedPoint2>? bestHealDict = null;

        foreach (var candidate in _body.GetWoundableTargets(body))
        {
            if (!TryCandidate(candidate, out var healDict, out var score))
                continue;

            if (score <= bestScore)
                continue;

            bestScore = score;
            best = candidate;
            bestHealDict = healDict;
        }

        if (best is not { } bestWoundable || bestHealDict == null)
            return false;

        usedFallback = !selected.IsValid() || bestWoundable != selected;
        woundable = bestWoundable;
        stuffToHeal = bestHealDict;
        return true;
    }

    public bool TryResolveTourniquetTarget(
        EntityUid body,
        EntityUid user,
        IReadOnlyCollection<BodyPartType> blockedParts,
        [NotNullWhen(true)] out EntityUid woundable)
    {
        woundable = EntityUid.Invalid;

        if (!TryComp<TargetingComponent>(user, out var targeting))
            return false;

        var (partType, symmetry) = _body.ConvertTargetBodyPart(targeting.Target);

        EntityUid? best = null;
        var bestBleeds = FixedPoint2.Zero;

        foreach (var candidate in _body.GetWoundableTargets(body))
        {
            if (!TryComp<WoundableComponent>(candidate, out var woundableComp))
                continue;

            if (!_body.TryGetWoundableBodyPartInfo(candidate, out _, out var candidateType, out _)
                || blockedParts.Contains(candidateType))
                continue;

            var bleeds = GetTotalBleeds(candidate, woundableComp);
            if (bleeds <= bestBleeds)
                continue;

            bestBleeds = bleeds;
            best = candidate;
        }

        if (_body.TryGetWoundableTargetByType(body, partType, symmetry, out var selected)
            && !blockedParts.Contains(partType)
            && TryComp<WoundableComponent>(selected, out var selectedComp))
        {
            var selectedBleeds = GetTotalBleeds(selected, selectedComp);
            if (selectedBleeds > 0)
            {
                woundable = selected;
                return true;
            }
        }

        if (best is not { } bestWoundable || bestBleeds <= 0)
            return false;

        woundable = bestWoundable;
        return true;
    }
    // end-backmen: medical-targeting
}
