using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.FixedPoint;

namespace Content.Shared.Backmen.Surgery.Traumas.Systems;

public partial class TraumaSystem
{
    #region Public API

    public bool TryApplyTrauma(EntityUid target, FixedPoint2 severity, WoundableComponent? woundable = null)
    {
        if (!Resolve(target, ref woundable) || _net.IsClient)
            return false;

        var globalTraumaApplied = false;

        if (RandomBoneTraumaChance(woundable))
        {
            var damageDelta = severity * _boneDamageMultipliers[woundable.WoundableSeverity];
            var traumaApplied = ApplyDamageToBone(woundable.Bone!.ContainedEntities[0], damageDelta);

            if (damageDelta > 0)
            {
                _sawmill.Info(
                    traumaApplied
                        ? $"A new trauma (Raw Severity: {severity}) was created on target: {target}. Type: Bone damage."
                        : $"Tried to create a trauma on target: {target}, but no trauma was applied. Type: Bone damage.");

                if (traumaApplied)
                    globalTraumaApplied = true;
            }
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

        // TODO: Veins, organs etc damage here.

        return globalTraumaApplied;
    }

    #endregion
}
