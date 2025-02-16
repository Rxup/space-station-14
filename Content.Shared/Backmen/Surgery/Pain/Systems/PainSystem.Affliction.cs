using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;

namespace Content.Shared.Backmen.Surgery.Pain.Systems;

public partial class PainSystem
{
    private void InitAffliction()
    {
        // Pain management hooks.
        SubscribeLocalEvent<PainInflicterComponent, WoundAddedEvent>(OnPainAdded);
        SubscribeLocalEvent<PainInflicterComponent, WoundSeverityPointChangedEvent>(OnPainChanged);
    }

    public const string PainModifierIdentifier = "Pain";

    #region Event Handling

    private void OnPainAdded(EntityUid uid, PainInflicterComponent pain, ref WoundAddedEvent args)
    {
        if (_net.IsClient)
            return;

        if (!TryComp<BodyPartComponent>(args.Woundable.RootWoundable, out var bodyPart))
            return;

        if (bodyPart.Body == null)
            return;

        var brainUid = GetNerveSystem(bodyPart.Body);
        if (!brainUid.HasValue)
            return;

        pain.Pain = FixedPoint2.Clamp(
            args.Component.WoundSeverityPoint * _painMultipliers[args.Component.WoundSeverity],
            0,
            100);

        if (!TryChangePainModifier(brainUid.Value, args.Component.HoldingWoundable, PainModifierIdentifier, pain.Pain))
        {
            TryAddPainModifier(brainUid.Value, args.Component.HoldingWoundable, PainModifierIdentifier, pain.Pain);
        }
    }

    private void OnPainChanged(EntityUid uid, PainInflicterComponent pain, WoundSeverityPointChangedEvent args)
    {
        if (_net.IsClient)
            return;

        if (!TryComp<BodyPartComponent>(args.Component.HoldingWoundable, out var bodyPart))
            return;

        if (bodyPart.Body == null)
            return;

        var brainUid = GetNerveSystem(bodyPart.Body);
        if (!brainUid.HasValue)
            return;

        var rootPart = Comp<BodyComponent>(bodyPart.Body.Value).RootContainer.ContainedEntity;
        if (!rootPart.HasValue)
            return;

        // bro how
        pain.Pain = FixedPoint2.Clamp(args.NewSeverity * _painMultipliers[args.Component.WoundSeverity], 0, 100);
        var allPain = (FixedPoint2) 0;

        foreach (var (_, comp) in _wound.GetAllWoundableChildren(rootPart.Value))
        {
            foreach (var woundId in comp.Wounds!.ContainedEntities)
            {
                if (!TryComp<PainInflicterComponent>(woundId, out var painInflicter))
                    continue;

                allPain += painInflicter.Pain;
            }
        }

        TryChangePainModifier(brainUid.Value, args.Component.HoldingWoundable, PainModifierIdentifier, allPain);
    }

    #endregion
}
