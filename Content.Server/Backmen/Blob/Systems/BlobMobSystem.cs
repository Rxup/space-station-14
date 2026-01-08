using Content.Server.Backmen.Language;
using Content.Server.Chat.Systems;
using Content.Server.Radio;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Backmen.Blob;
using Content.Shared.Backmen.Blob.Components;
using Content.Shared.Backmen.EntityEffects.Effects;
using Content.Shared.Backmen.Language;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Body;
using Content.Shared.Radio.Components;
using Content.Shared.Speech;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Backmen.Blob.Systems;

public sealed class BlobMobSystem : SharedBlobMobSystem
{
    [Dependency] private readonly LanguageSystem _language = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly RadioSystem _radioSystem = default!;
    [Dependency] private readonly SharedEntityEffectsSystem _effectsSystem = default!;
    [Dependency] private readonly SharedBodySystem _bodySystem = default!;

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

    private static readonly EntityEffect[] HealingEffects =
    [
        new AdjustTraumas
        {
            Amount = -1,
            ModifierIdentifier = "BlobHeal",
            TraumaType = TraumaType.BoneDamage
        },
        new AdjustTraumas
        {
            Amount = -1,
            ModifierIdentifier = "BlobHeal",
            TraumaType = TraumaType.VeinsDamage
        },
        new AdjustTraumas
        {
            Amount = -1,
            ModifierIdentifier = "BlobHeal",
            TraumaType = TraumaType.NerveDamage
        },
        new AdjustTraumas
        {
            Amount = -1,
            ModifierIdentifier = "BlobHeal",
            TraumaType = TraumaType.Dismemberment
        },
        new EyeDamage()
    ];
    private void OnPulsed(EntityUid uid, BlobMobComponent component, BlobMobGetPulseEvent args)
    {
        _damageableSystem.ChangeDamage(uid, component.HealthOfPulse, targetPart: TargetBodyPart.All);
        _effectsSystem.ApplyEffects(uid, HealingEffects); // healing wounds

        if(component.CureBodyInterval <= 0)
            return;

        // blob restore body part
        component.CureTick++;
        if (component.CureTick > component.CureBodyInterval)
        {
            component.CureTick = 0;
            _bodySystem.ForceRestoreBody(uid, false);
        }
    }
}
