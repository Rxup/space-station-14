using Content.Shared.Backmen.Surgery.Body.Events;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.Backmen.Surgery.Pain.Systems;

[Virtual]
public partial class PainSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    [Dependency] private readonly SharedBodySystem _body = default!;

    [Dependency] private readonly SharedAudioSystem _IHaveNoMouthAndIMustScream = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    [Dependency] private readonly StandingStateSystem _standing = default!;

    [Dependency] private readonly WoundSystem _wound = default!;
    [Dependency] private readonly ConsciousnessSystem _consciousness = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("pain");

        SubscribeLocalEvent<BodyComponent, BodyPartAddedEvent>(OnBodyPartAdded);
        SubscribeLocalEvent<BodyComponent, BodyPartRemovedEvent>(OnBodyPartRemoved);

        SubscribeLocalEvent<NerveSystemComponent, MobStateChangedEvent>(OnMobStateChanged);

        InitAffliction();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateDamage(frameTime);
    }

    private void OnBodyPartAdded(EntityUid uid, BodyComponent body, ref BodyPartAddedEvent args)
    {
        if (_net.IsClient)
            return;

        var brainUid = GetNerveSystem(uid);
        if (!brainUid.HasValue || TerminatingOrDeleted(brainUid.Value))
            return;

        TryRemovePainMultiplier(brainUid.Value, MetaData(args.Part.Owner).EntityPrototype!.ID + "Loss");
        UpdateNerveSystemNerves(brainUid.Value, args.Part.Comp.Body!.Value, Comp<NerveSystemComponent>(brainUid.Value));
    }

    private void OnBodyPartRemoved(EntityUid uid, BodyComponent body, ref BodyPartRemovedEvent args)
    {
        if (_net.IsClient)
            return;

        var brainUid = GetNerveSystem(uid);
        if (!brainUid.HasValue || TerminatingOrDeleted(brainUid.Value))
            return;

        TryAddPainMultiplier(brainUid.Value, MetaData(args.Part.Owner).EntityPrototype!.ID + "Loss", 2);
        UpdateNerveSystemNerves(brainUid.Value, args.Part.Comp.Body!.Value, Comp<NerveSystemComponent>(brainUid.Value));
    }

    private void OnMobStateChanged(EntityUid uid, NerveSystemComponent nerveSys, MobStateChangedEvent args)
    {
        switch (args.NewMobState)
        {
            case MobState.Critical:
                var sex = Sex.Unsexed;
                if (TryComp<HumanoidAppearanceComponent>(args.Target, out var humanoid))
                    sex = humanoid.Sex;

                CleanupSounds(nerveSys);
                PlayPainSound(args.Target, nerveSys, nerveSys.CritWhimpers[sex], AudioParams.Default.WithVolume(-12f));
                break;
            default:
                CleanupSounds(nerveSys);
                break;
        }
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
