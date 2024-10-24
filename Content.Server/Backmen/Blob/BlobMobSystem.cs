using System.Numerics;
using Content.Server.Backmen.Blob.Components;
using Content.Server.Backmen.Language;
using Content.Server.Backmen.Language.Events;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Popups;
using Content.Server.Radio;
using Content.Server.Radio.Components;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Backmen.Blob;
using Content.Shared.Backmen.Blob.Chemistry;
using Content.Shared.Backmen.Blob.Components;
using Content.Shared.Backmen.Language;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Blob;

public sealed class BlobMobSystem : SharedBlobMobSystem
{
    [Dependency] private readonly LanguageSystem _language = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly RadioSystem _radioSystem = default!;
    private EntityQuery<BlobSpeakComponent> _activeBSpeak;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobMobComponent, BlobMobGetPulseEvent>(OnPulsed);

        SubscribeLocalEvent<BlobSpeakComponent, DetermineEntityLanguagesEvent>(OnLanguageApply);
        SubscribeLocalEvent<BlobSpeakComponent, ComponentStartup>(OnSpokeAdd);
        SubscribeLocalEvent<BlobSpeakComponent, ComponentShutdown>(OnSpokeRemove);
        SubscribeLocalEvent<BlobSpeakComponent, TransformSpeakerNameEvent>(OnSpokeName);
        SubscribeLocalEvent<BlobSpeakComponent, SpeakAttemptEvent>(OnSpokeCan, after: new []{ typeof(SpeechSystem) });
        SubscribeLocalEvent<BlobSpeakComponent, EntitySpokeEvent>(OnSpoke, before: new []{ typeof(RadioSystem), typeof(HeadsetSystem) });
        SubscribeLocalEvent<BlobSpeakComponent, RadioReceiveEvent>(OnIntrinsicReceive);
        //SubscribeLocalEvent<SmokeOnTriggerComponent, TriggerEvent>(HandleSmokeTrigger);

        _activeBSpeak = GetEntityQuery<BlobSpeakComponent>();
    }

    private void OnIntrinsicReceive(Entity<BlobSpeakComponent> ent, ref RadioReceiveEvent args)
    {
        if (TryComp(ent, out ActorComponent? actor) && args.Channel.ID == ent.Comp.Channel)
        {
            _netMan.ServerSendMessage(args.ChatMsg, actor.PlayerSession.Channel);
        }
    }

    private void OnSpoke(Entity<BlobSpeakComponent> ent, ref EntitySpokeEvent args)
    {
        if(args.Channel == null)
            return;
        _radioSystem.SendRadioMessage(ent, args.Message, ent.Comp.Channel, ent, language: args.Language);
    }

    private void OnLanguageApply(Entity<BlobSpeakComponent> ent, ref DetermineEntityLanguagesEvent args)
    {
        if(ent.Comp.LifeStage is
           ComponentLifeStage.Removing
           or ComponentLifeStage.Stopping
           or ComponentLifeStage.Stopped)
            return;

        args.SpokenLanguages.Clear();
        args.SpokenLanguages.Add(ent.Comp.Language);
        args.UnderstoodLanguages.Add(ent.Comp.Language);
    }

    private void OnSpokeName(Entity<BlobSpeakComponent> ent, ref TransformSpeakerNameEvent args)
    {
        if (!ent.Comp.OverrideName)
        {
            return;
        }
        args.VoiceName = Loc.GetString(ent.Comp.Name);
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
        if(TerminatingOrDeleted(ent))
            return;

        _language.UpdateEntityLanguages(ent.Owner);
        var radio = EnsureComp<ActiveRadioComponent>(ent);
        radio.Channels.Remove(ent.Comp.Channel);
    }

    private void OnSpokeAdd(Entity<BlobSpeakComponent> ent, ref ComponentStartup args)
    {
        if(TerminatingOrDeleted(ent))
            return;

        var component = EnsureComp<LanguageSpeakerComponent>(ent);
        component.CurrentLanguage = ent.Comp.Language;
        _language.UpdateEntityLanguages(ent.Owner);

        var radio = EnsureComp<ActiveRadioComponent>(ent);
        radio.Channels.Add(ent.Comp.Channel);
    }

    private void OnPulsed(EntityUid uid, BlobMobComponent component, BlobMobGetPulseEvent args)
    {
        _damageableSystem.TryChangeDamage(uid, component.HealthOfPulse);
    }


}
