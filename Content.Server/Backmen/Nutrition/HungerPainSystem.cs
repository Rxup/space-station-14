using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.Nutrition;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Surgery.Pain;
using Content.Shared.Backmen.Surgery.Pain.Systems;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Backmen.Surgery.Traumas.Systems;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Player;

namespace Content.Server.Backmen.Nutrition;

public sealed partial class HungerPainSystem : EntitySystem
{
    [Dependency] private BkmBodySharedSystem _body = default!;
    [Dependency] private ConsciousnessSystem _consciousness = default!;
    [Dependency] private HungerSystem _hunger = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PainSystem _pain = default!;
    [Dependency] private TraumaSystem _trauma = default!;

    private EntityQuery<ActorComponent> _actorQuery;

    public const string PainStarvingModifierIdentifier = "Starving";

    public override void Initialize()
    {
        base.Initialize();
        _actorQuery = GetEntityQuery<ActorComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HungerComponent>();
        while (query.MoveNext(out var uid, out var hunger))
        {
            if (!hunger.StarvingPainEnabled)
                continue;

            if (_mobState.IsDead(uid) || !_actorQuery.HasComp(uid))
            {
                if (TryComp<HungerPainTrackerComponent>(uid, out var inactiveTracker))
                    ClearStarvingPain(uid, inactiveTracker);
                continue;
            }

            var tracker = EnsureComp<HungerPainTrackerComponent>(uid);

            if (!_consciousness.TryGetNerveSystem(uid, out var nerveSys))
            {
                ClearStarvingPain(uid, tracker);
                continue;
            }

            if (!_body.TryGetWoundableTargetByType(uid, BodyPartType.Chest, null, out var chest))
                continue;

            var hungerValue = _hunger.GetHunger(hunger);
            var starvingThreshold = hunger.Thresholds[HungerThreshold.Starving];

            if (hunger.CurrentThreshold <= HungerThreshold.Starving)
            {
                var severity = 1f - hungerValue / starvingThreshold;
                severity = Math.Clamp(severity, 0f, 1f);
                var targetPain = severity * hunger.StarvingPainMax;

                if (tracker.CurrentStarvingPain < targetPain)
                {
                    tracker.CurrentStarvingPain = Math.Min(
                        targetPain,
                        tracker.CurrentStarvingPain + hunger.StarvingPainGrowthRate * frameTime);
                }
                else
                {
                    tracker.CurrentStarvingPain = targetPain;
                }

                ApplyStarvingPain(nerveSys.Value, chest, tracker);

                if (hungerValue < hunger.StarvingOrganTraumaThreshold && !tracker.StarvingOrganTraumaApplied)
                {
                    tracker.StarvingOrganTraumaApplied = true;
                    _trauma.TryAddOrganDamageModifier(
                        chest,
                        FixedPoint2.New(5),
                        uid,
                        "StarvingOrganTrauma");
                }
            }
            else
            {
                if (tracker.CurrentStarvingPain > 0)
                {
                    tracker.CurrentStarvingPain = Math.Max(
                        0,
                        tracker.CurrentStarvingPain - hunger.StarvingPainDecayRate * frameTime);

                    if (tracker.CurrentStarvingPain <= 0)
                    {
                        ClearStarvingPain(uid, tracker, nerveSys.Value, chest);
                    }
                    else
                    {
                        ApplyStarvingPain(nerveSys.Value, chest, tracker);
                    }
                }
                else
                {
                    tracker.StarvingOrganTraumaApplied = false;
                }
            }

            Dirty(uid, tracker);
        }
    }

    public void ResetStarvingPain(EntityUid body, float remainingPain = 0)
    {
        if (!TryComp<HungerPainTrackerComponent>(body, out var tracker))
            return;

        tracker.CurrentStarvingPain = remainingPain;
        Dirty(body, tracker);
    }

    private void ApplyStarvingPain(EntityUid nerveSys, EntityUid chest, HungerPainTrackerComponent tracker)
    {
        var pain = FixedPoint2.New(tracker.CurrentStarvingPain);

        if (!_pain.TryChangePainModifier(
                nerveSys,
                chest,
                PainStarvingModifierIdentifier,
                pain,
                painType: PainType.Starving))
        {
            _pain.TryAddPainModifier(
                nerveSys,
                chest,
                PainStarvingModifierIdentifier,
                pain,
                PainType.Starving);
        }
    }

    private void ClearStarvingPain(
        EntityUid uid,
        HungerPainTrackerComponent tracker,
        EntityUid? nerveSys = null,
        EntityUid? chest = null)
    {
        if (tracker.CurrentStarvingPain <= 0 && !tracker.StarvingOrganTraumaApplied)
            return;

        EntityUid nerveUid;
        if (nerveSys != null)
            nerveUid = nerveSys.Value;
        else if (!_consciousness.TryGetNerveSystem(uid, out var nerve))
            return;
        else
            nerveUid = nerve.Value;

        EntityUid chestUid;
        if (chest != null)
            chestUid = chest.Value;
        else if (!_body.TryGetWoundableTargetByType(uid, BodyPartType.Chest, null, out var chestWoundable))
            return;
        else
            chestUid = chestWoundable;

        _pain.TryRemovePainModifier(nerveUid, chestUid, PainStarvingModifierIdentifier);

        if (tracker.StarvingOrganTraumaApplied)
        {
            _trauma.TryRemoveOrganDamageModifier(chestUid, uid, "StarvingOrganTrauma");
            tracker.StarvingOrganTraumaApplied = false;
        }

        tracker.CurrentStarvingPain = 0;
        Dirty(uid, tracker);
    }
}
