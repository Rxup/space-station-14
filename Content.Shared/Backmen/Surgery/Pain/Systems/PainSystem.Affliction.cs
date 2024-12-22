using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Backmen.Surgery.Wounds;
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

    #region Event Handling

    private void OnPainAdded(EntityUid uid, PainInflicterComponent pain, WoundAddedEvent args)
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
            args.Component.WoundSeverityPoint * _painMultipliers[args.Component.WoundSeverity] / 3,
            0,
            100);

        if (!TryChangePainModifier(brainUid.Value, args.Component.Parent, pain.Pain))
        {
            TryAddPainModifier(brainUid.Value, args.Component.Parent, pain.Pain);
        }
    }

    private void OnPainChanged(EntityUid uid, PainInflicterComponent pain, WoundSeverityPointChangedEvent args)
    {
        if (_net.IsClient)
            return;

        if (!TryComp<BodyPartComponent>(args.Component.Parent, out var bodyPart))
            return;

        if (bodyPart.Body == null)
            return;

        var brainUid = GetNerveSystem(bodyPart.Body);
        if (!brainUid.HasValue)
            return;

        // bro how
        pain.Pain += FixedPoint2.Clamp(args.NewSeverity * _painMultipliers[args.Component.WoundSeverity] / 3, 0, 100);
        TryChangePainModifier(brainUid.Value, args.Component.Parent, args.NewSeverity * _painMultipliers[args.Component.WoundSeverity] / 3);
    }

    #endregion
}
