using System.Linq;
using Content.Shared.Armor;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Robust.Shared.Random;

namespace Content.Shared.Backmen.Surgery.Traumas.Systems;

public partial class TraumaSystem
{
    /// <summary>
    /// СЛАВА БОГУ 42🙏❤️СЛАВА 42🙏❤️АНГЕЛА ХРАНИТЕЛЯ 42 КАЖДОМУ ИЗ ВАС🙏❤️БОЖЕ ХРАНИ 42🙏❤️СПАСИБО ВАМ НАШИ БРАТУХИ🙏🏼❤️ХРАНИ 42💯СЛАВА БОГУ 42🙏❤️СЛАВА БОГУ 42🙏❤️
    /// СЛАВА 42🙏❤️АНГЕЛА ХРАНИТЕЛЯ 42 КАЖДОМУ СЛАВА БОГУ СВО 🇷🇺❤️ СЛАВА РОССИИ 🇷🇺❤️
    /// АНГЕЛА ХРАНИТЕЛЯ КАЖДОМУ ИЗ ВАС 🇷🇺❤️БОЖЕ ХРАНИ РОССИЮ 🇷🇺❤️ СПАСИБО ВАМ НАШИ МАЛЬЧИКИ ЧТО ПОДДЕРЖИВАЕТЕ РОССИЮ 🇷🇺❤️
    /// СВОих не бросаем! 🇷🇺❤️ ZOV 🇷🇺❤️ ГОЙДА ВСЕМ 🇷🇺🇷🇺 ГОЙДА 🇷🇺❤️ ZOV 🇷🇺❤️ СПАСИБО НАШИМ РОДНЫМ 🇷🇺🇷🇺 ДАЙ БОГ БОГ ВАМ ЗДОРОВЬЯ 🇷🇺🇷🇺 СМАРТФОН VIVO 🇷🇺🇷🇺💪💪
    /// ЖЕЛАЮ ЧТОБ ПРИШЛИ ЗДОРОВЫМИ С ПОБЕДОЙ 🇷🇺🇷🇺🙏 ГОЙДА БРАТЬЯ 🇷🇺🇷🇺
    /// </summary>
    private const float DismembermentChanceMultiplier = 0.042f;
    private const float NerveDamageChanceMultiplier = 0.032f;

    // In seconds
    private const float NerveDamageMultiplierTime = 8f;

    #region Public API

    public IEnumerable<TraumaType> RandomTraumaChance(EntityUid target, EntityUid woundInflicter, FixedPoint2 severity, WoundableComponent? woundable = null)
    {
        var traumaList = new HashSet<TraumaType>();
        if (!Resolve(target, ref woundable) || _net.IsClient)
            return traumaList;

        if (RandomBoneTraumaChance(woundable, woundInflicter))
            traumaList.Add(TraumaType.BoneDamage);

        if (RandomNerveDamageChance(target, woundInflicter, woundable, severity))
            traumaList.Add(TraumaType.NerveDamage);

        //if (RandomOrganTraumaChance(woundable))
        //    traumaList.Add(TraumaType.OrganDamage);
        //
        //if (RandomVeinsTraumaChance(woundable))
        //    traumaList.Add(TraumaType.VeinsDamage);

        if (RandomDismembermentTraumaChance(target, woundInflicter, woundable, severity))
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

    public bool TryApplyTraumaWithRandom(EntityUid target, EntityUid woundInflicter, FixedPoint2 severity, WoundableComponent? woundable = null)
    {
        if (!Resolve(target, ref woundable) || _net.IsClient)
            return false;

        if (severity <= 0)
            return false;

        var traumas = RandomTraumaChance(target, woundInflicter, severity, woundable).ToList();
        if (traumas.Count == 0)
            return false;

        ApplyTraumas(target, traumas, severity, woundable);
        return true;
    }

    public FixedPoint2 GetCoverageDeduction(EntityUid body, BodyPartType coverage)
    {
        var deduction = (FixedPoint2) 0;

        foreach (var ent in _inventory.GetHandOrInventoryEntities(body, SlotFlags.WITHOUT_POCKET))
        {
            if (!TryComp<ArmorComponent>(ent, out var armour))
                continue;

            if (armour.ArmorCoverage.Contains(coverage))
            {
                deduction += armour.DismembermentChanceDeduction;
            }
        }

        return deduction;
    }

    public bool RandomNerveDamageChance(EntityUid target, EntityUid woundInflicter, WoundableComponent woundable, FixedPoint2 severity)
    {
        var bodyPart = Comp<BodyPartComponent>(target);
        if (!bodyPart.Body.HasValue)
            return false; // No body entity to apply pain to

        // dawg how
        if (woundable.WoundableIntegrity <= 0)
            return false;

        // literally dismemberment chance, but lower by default
        var chance =
            FixedPoint2.Clamp(
                woundable.WoundableIntegrity / woundable.IntegrityCap
                * NerveDamageChanceMultiplier + Comp<WoundComponent>(woundInflicter).TraumasChances[TraumaType.NerveDamage],
                0,
                1);

        return _random.Prob((float) chance);
    }

    public bool RandomDismembermentTraumaChance(EntityUid target, EntityUid woundInflicter, WoundableComponent woundable, FixedPoint2 severity)
    {
        var bodyPart = Comp<BodyPartComponent>(target);
        if (!bodyPart.Body.HasValue)
            return false; // Can't sever if already severed

        var parentWoundable = woundable.ParentWoundable;
        if (!parentWoundable.HasValue) // how?
            return false;

        var parentComp = Comp<WoundableComponent>(parentWoundable.Value);
        if ((parentComp.WoundableIntegrity == parentComp.IntegrityCap || woundable.WoundableIntegrity == woundable.IntegrityCap) && severity < 21)
            return false; // just so you don't get your body part ripped out by a sneeze

        if (Comp<BodyPartComponent>(target).PartType == BodyPartType.Groin && parentComp.WoundableSeverity is not WoundableSeverity.Critical)
            return false;

        var deduction = GetCoverageDeduction(bodyPart.Body.Value, bodyPart.PartType);

        // random-y but not so random-y like bones. Heavily depends on woundable state and damage
        var chance =
            FixedPoint2.Clamp(
                woundable.WoundableIntegrity / woundable.IntegrityCap
                * DismembermentChanceMultiplier - deduction + Comp<WoundComponent>(woundInflicter).TraumasChances[TraumaType.Dismemberment],
                0,
                1);
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

            var bodyPart = Comp<BodyPartComponent>(target);
            if (bodyPart.Body.HasValue && _conciousness.TryGetNerveSystem(bodyPart.Body.Value, out var nerveSys))
                _pain.TryAddPainModifier(nerveSys.Value, target, "BoneDamageImminent", 20f, time: TimeSpan.FromSeconds(12f));

            _sawmill.Info(traumaApplied
                ? $"A new trauma (Raw Severity: {severity}) was created on target: {target}. Type: Bone damage."
                : $"Tried to create a trauma on target: {target}, but no trauma was applied. Type: Bone damage.");
        }

        if (traumaList.Contains(TraumaType.NerveDamage))
        {
            var bodyPart = Comp<BodyPartComponent>(target);
            if (bodyPart.Body.HasValue && _conciousness.TryGetNerveSystem(bodyPart.Body.Value, out var nerveSys))
            {
                _pain.TryAddPainMultiplier(nerveSys.Value,
                    "NerveDamage",
                    2f,
                    time: TimeSpan.FromSeconds(NerveDamageMultiplierTime));
                _pain.TryAddPainFeelsModifier(nerveSys.Value,
                    "NerveDamage",
                    target,
                    -0.4f);

                foreach (var child in _wound.GetAllWoundableChildren(target))
                {
                    _pain.TryAddPainFeelsModifier(nerveSys.Value,
                        "NerveDamage",
                        child.Item1,
                        -0.4f);
                }

                _sawmill.Info( $"A new trauma (Caused by {severity} damage) was created on target: {target}. Type: NerveDamage.");
            }
        }

        // TODO: veins, would have been very lovely to integrate this into vascular system
        //if (RandomVeinsTraumaChance(woundable))
        //{
        //    traumaApplied = ApplyDamageToVeins(woundable.Veins!.ContainedEntities[0], severity * _veinsDamageMultipliers[woundable.WoundableSeverity]);
        //    _sawmill.Info(traumaApplied
        //        ? $"A new trauma (Raw Severity: {severity}) was created on target: {target} of type Vein damage"
        //        : $"Tried to create a trauma on target: {target}, but no trauma was applied. Type: Vein damage.");
        //}

        if (traumaList.Contains(TraumaType.Dismemberment))
        {
            if (!_wound.IsWoundableRoot(target, woundable) && woundable.ParentWoundable.HasValue)
            {
                _wound.AmputateWoundable(woundable.ParentWoundable.Value, target, woundable);
                var bodyPart = Comp<BodyPartComponent>(target);
                if (bodyPart.Body.HasValue && _conciousness.TryGetNerveSystem(bodyPart.Body.Value, out var nerveSys))
                    _pain.TryAddPainModifier(nerveSys.Value, target, "Dismemberment", 20f, time: TimeSpan.FromSeconds(12f));

                _sawmill.Info( $"A new trauma (Caused by {severity} damage) was created on target: {target}. Type: Dismemberment.");
            }
        }
    }

    #endregion
}
