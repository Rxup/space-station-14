using Content.Shared.Backmen.Surgery.Body.Events;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Robust.Shared.Network;

namespace Content.Shared.Backmen.Surgery.Pain.Systems;

[Virtual]
public partial class PainSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("pain");

        SubscribeLocalEvent<BodyComponent, BodyPartAddedEvent>(OnBodyPartAdded);
        SubscribeLocalEvent<BodyComponent, BodyPartRemovedEvent>(OnBodyPartRemoved);

        InitAffliction();
    }

    private void OnBodyPartAdded(EntityUid uid, BodyComponent body, ref BodyPartAddedEvent args)
    {
        if (_net.IsClient)
            return;

        var brainUid = EntityUid.Invalid;
        foreach (var organ in _body.GetBodyOrgans(args.Part.Comp.Body))
        {
            if (!TryComp<NerveSystemComponent>(organ.Id, out _))
                continue;
            brainUid = organ.Id;
        }

        if (brainUid == EntityUid.Invalid)
            return;

        UpdateNerveSystemNerves(brainUid, args.Part.Comp.Body!.Value, Comp<NerveSystemComponent>(brainUid));
    }

    private void OnBodyPartRemoved(EntityUid uid, BodyComponent body, ref BodyPartRemovedEvent args)
    {
        if (_net.IsClient)
            return;

        var brainUid = EntityUid.Invalid;
        foreach (var organ in _body.GetBodyOrgans(args.Part.Comp.Body))
        {
            if (!TryComp<NerveSystemComponent>(organ.Id, out _))
                continue;
            brainUid = organ.Id;
        }

        if (brainUid == EntityUid.Invalid)
            return;

        UpdateNerveSystemNerves(brainUid, args.Part.Comp.Body!.Value, Comp<NerveSystemComponent>(brainUid));
    }

    private void UpdateNerveSystemNerves(EntityUid uid, EntityUid body, NerveSystemComponent component)
    {
        component.Nerves.Clear();
        foreach (var bodyPart in _body.GetBodyChildren(body))
        {
            if (!TryComp<NerveComponent>(bodyPart.Id, out var nerve))
                continue;

            component.Nerves.Add(bodyPart.Id, nerve);
            Dirty(uid, component);

            nerve.ParentedNerveSystem = uid;
            Dirty(bodyPart.Id, nerve); // ヾ(≧▽≦*)o
        }

        _sawmill.Info($"Nerve system's (uid: {uid}) nerves updated on body (uid: {body}). Current nerves count: {component.Nerves.Count}");
    }
}
