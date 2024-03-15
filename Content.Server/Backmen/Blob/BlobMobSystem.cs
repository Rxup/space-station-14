using Content.Server.Backmen.Blob.Components;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Popups;
using Content.Server.Radio.Components;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Backmen.Blob;
using Content.Shared.Backmen.Blob.Chemistry;
using Content.Shared.Backmen.Blob.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Blob;

public sealed class BlobMobSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    //[Dependency] private readonly SmokeSystem _smokeSystem = default!;
    //[Dependency] private readonly SharedAudioSystem _audioSystem = default!;

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly RadioSystem _radioSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobMobComponent, BlobMobGetPulseEvent>(OnPulsed);
        SubscribeLocalEvent<BlobMobComponent, AttackAttemptEvent>(OnBlobAttackAttempt);
        SubscribeLocalEvent<BlobSpeakComponent, EntitySpokeEvent>(OnSpoke, before: new []{ typeof(RadioSystem) });
        SubscribeLocalEvent<BlobSpeakComponent, ComponentStartup>(OnSpokeAdd);
        SubscribeLocalEvent<BlobSpeakComponent, ComponentShutdown>(OnSpokeRemove);
        SubscribeLocalEvent<BlobSpeakComponent, TransformSpeakerNameEvent>(OnSpokeName);
        SubscribeLocalEvent<BlobSpeakComponent, SpeakAttemptEvent>(OnSpokeCan, after: new []{ typeof(SpeechSystem) });
        //SubscribeLocalEvent<SmokeOnTriggerComponent, TriggerEvent>(HandleSmokeTrigger);
    }

    private void OnSpokeName(Entity<BlobSpeakComponent> ent, ref TransformSpeakerNameEvent args)
    {
        if (HasComp<BlobCarrierComponent>(ent))
        {
            return;
        }
        args.Name = "Блоб";
    }

    private void OnSpokeCan(Entity<BlobSpeakComponent> ent, ref SpeakAttemptEvent args)
    {
        if (HasComp<BlobCarrierComponent>(ent))
        {
            return;
        }
        args.Uncancel();
    }

    private void OnSpokeRemove(Entity<BlobSpeakComponent> ent, ref ComponentShutdown args)
    {
        var radio = EnsureComp<ActiveRadioComponent>(ent);
        radio.Channels.Remove(ent.Comp.Channel);
        var snd = EnsureComp<IntrinsicRadioTransmitterComponent>(ent);
        snd.Channels.Remove(ent.Comp.Channel);
    }

    private void OnSpokeAdd(Entity<BlobSpeakComponent> ent, ref ComponentStartup args)
    {
        EnsureComp<IntrinsicRadioReceiverComponent>(ent);
        var radio = EnsureComp<ActiveRadioComponent>(ent);
        radio.Channels.Add(ent.Comp.Channel);
        var snd = EnsureComp<IntrinsicRadioTransmitterComponent>(ent);
        snd.Channels.Add(ent.Comp.Channel);
    }


    private void OnSpoke(Entity<BlobSpeakComponent> ent, ref EntitySpokeEvent args)
    {
        if (args.Channel == null)
            args.Channel = _prototypeManager.Index(ent.Comp.Channel);

        if (!TryComp<IntrinsicRadioTransmitterComponent>(ent, out var component) ||
            !component.Channels.Contains(args.Channel.ID) ||
            args.Channel.ID != ent.Comp.Channel)
        {
            return;
        }

        if (TryComp<BlobObserverComponent>(ent, out var blobObserverComponent) && blobObserverComponent.Core.HasValue)
        {
            _radioSystem.SendRadioMessage(blobObserverComponent.Core.Value, args.OriginalMessage, args.Channel, blobObserverComponent.Core.Value);
        }
        else
        {
            _radioSystem.SendRadioMessage(ent, args.OriginalMessage, args.Channel, ent);
        }

        args.Channel = null; // prevent duplicate messages from other listeners.
    }

    private void OnPulsed(EntityUid uid, BlobMobComponent component, BlobMobGetPulseEvent args)
    {
        _damageableSystem.TryChangeDamage(uid, component.HealthOfPulse);
    }

    private void OnBlobAttackAttempt(EntityUid uid, BlobMobComponent component, AttackAttemptEvent args)
    {
        if (args.Cancelled || !HasComp<BlobTileComponent>(args.Target) && !HasComp<BlobMobComponent>(args.Target))
            return;

        // TODO: Move this to shared
        _popupSystem.PopupCursor(Loc.GetString("blob-mob-attack-blob"), uid, PopupType.Large);
        args.Cancel();
    }

/*
    private void HandleSmokeTrigger(EntityUid uid, SmokeOnTriggerComponent comp, TriggerEvent args)
    {
        var xform = Transform(uid);
        var smokeEnt = Spawn("Smoke", xform.Coordinates);
        var smoke = EnsureComp<SmokeComponent>(smokeEnt);
        var colored = EnsureComp<BlobSmokeColorComponent>(smokeEnt);
        colored.Color = comp.SmokeColor;
        //colored.SmokeColor = comp.SmokeColor;
        Dirty(smokeEnt,smoke);
        var solution = new Solution();
        foreach (var reagent in comp.SmokeReagents)
        {
            solution.AddReagent(reagent.Reagent, reagent.Quantity);
        }
        _smokeSystem.StartSmoke(smokeEnt, solution, comp.Time, comp.SpreadAmount, smoke);
        _audioSystem.PlayPvs(comp.Sound, xform.Coordinates, AudioParams.Default.WithVariation(0.125f));
        args.Handled = true;
    }*/
}
