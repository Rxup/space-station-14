using System.Linq;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Random;

namespace Content.Shared.Backmen.Surgery.Traumas.Systems;

public partial class TraumaSystem
{
    private const float DismembermentChanceMultiplier = 0.04f;

    #region Public API

    public IEnumerable<TraumaType> RandomTraumaChance(EntityUid target, FixedPoint2 severity, WoundableComponent? woundable = null)
    {
        var traumaList = new HashSet<TraumaType>();
        if (!Resolve(target, ref woundable) || _net.IsClient)
            return traumaList;

        if (RandomBoneTraumaChance(woundable))
            traumaList.Add(TraumaType.BoneDamage);

        //if (RandomOrganTraumaChance(woundable))
        //    traumaList.Add(TraumaType.OrganDamage);
        //
        //if (RandomVeinsTraumaChance(woundable))
        //    traumaList.Add(TraumaType.VeinsDamage);

        if (RandomDismembermentTraumaChance(target, woundable, severity))
            traumaList.Add(TraumaType.Dismemberment);

        return traumaList;
    }

    public bool TryApplyTrauma(EntityUid target, FixedPoint2 severity, IEnumerable<TraumaType> traumas, WoundableComponent? woundable = null)
    {
        if (!Resolve(target, ref woundable) || _net.IsClient)
            return false;

        if (severity <= 0)
            return false;

        var traumaTypes = traumas.ToList();
        if (traumaTypes.Count == 0)
            return false;

        ApplyTraumas(target, traumaTypes, severity, woundable);
        return true;
    }

    public bool TryApplyTraumaWithRandom(EntityUid target, FixedPoint2 severity, WoundableComponent? woundable = null)
    {
        if (!Resolve(target, ref woundable) || _net.IsClient)
            return false;

        if (severity <= 0)
            return false;

        var traumas = RandomTraumaChance(target, severity, woundable).ToList();
        if (traumas.Count == 0)
            return false;

        ApplyTraumas(target, traumas, severity, woundable);
        return true;
    }

    public bool RandomDismembermentTraumaChance(EntityUid target, WoundableComponent woundable, FixedPoint2 severity)
    {
        // random-y but not so random-y like bones. Heavily depends on woundable state and damage
        var chance =
            woundable.WoundableIntegrity / (woundable.IntegrityCap - _wound.ApplySeverityModifiers(target, severity)) * DismembermentChanceMultiplier;
        // getting hit again increases the chance

        return _random.Prob((float) chance);
    }

    #endregion

    #region Private API

    private void ApplyTraumas(EntityUid target, IEnumerable<TraumaType> traumas, FixedPoint2 severity, WoundableComponent woundable)
    {
        var traumaList = traumas.ToList();
        if (traumaList.Contains(TraumaType.BoneDamage))
        {
            var damage = severity * _boneDamageMultipliers[woundable.WoundableSeverity];
            var traumaApplied = ApplyDamageToBone(woundable.Bone!.ContainedEntities[0], damage);

            _sawmill.Info(traumaApplied
                ? $"A new trauma (Raw Severity: {severity}) was created on target: {target}. Type: Bone damage."
                : $"Tried to create a trauma on target: {target}, but no trauma was applied. Type: Bone damage.");
        }

        // TODO: veins, would have been very lovely to integrate this into vascular system
        //if (RandomVeinsTraumaChance(woundable))
        //{
        //    traumaApplied = ApplyDamageToVeins(woundable.Veins!.ContainedEntities[0], severity * _veinsDamageMultipliers[woundable.WoundableSeverity]);
        //_sawmill.Info(
        //    traumaApplied
        //        ? $"A new trauma (Raw Severity: {severity}) was created on target: {target} of type bone damage"
        //        : $"Tried to create a trauma on target: {target}, but no trauma was applied. Type: Bone damage.");
        //
        //if (traumaApplied)
        //    globalTraumaApplied = true;
        //}

        if (traumaList.Contains(TraumaType.Dismemberment))
        {
            if (!_wound.IsWoundableRoot(target, woundable) && woundable.ParentWoundable.HasValue)
            {
                _wound.AmputateWoundable(woundable.ParentWoundable.Value, target, woundable);
                _sawmill.Info( $"A new trauma (Caused by {severity} damage) was created on target: {target}. Type: Dismemberment.");
            }
        }
    }

    #endregion
}
