using Content.Server.Bible;
using Content.Server.Bible.Components;
using Content.Server.Construction;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Kitchen.EntitySystems;
using Content.Server.Mind;
using Content.Server.Speech.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared._Impstation.Revenant;
using Content.Shared._Impstation.Revenant.Components;
using Content.Shared.Revenant;
using Content.Shared.Revenant.Components;
using Content.Shared.Chat;
using Content.Shared.Speech;
using Content.Shared.StatusEffectNew;
using Content.Shared.Tag;
using Robust.Shared.Player;

namespace Content.Server._Impstation.Revenant.EntitySystems;

public sealed partial class RevenantStasisSystem : EntitySystem
{
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private GhostRoleSystem _ghostRoles = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private ExplosionSystem _explosion = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevenantStasisComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<RevenantStasisComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<RevenantStasisStatusEffectComponent, StatusEffectRemovedEvent>(OnStasisStatusRemoved);
        SubscribeLocalEvent<RevenantStasisComponent, ChangeDirectionAttemptEvent>(OnAttemptDirection);
        SubscribeLocalEvent<RevenantStasisComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<RevenantStasisComponent, ConstructionConsumedObjectEvent>(OnCrafted);
        SubscribeLocalEvent<RevenantStasisComponent, ReagentGrinderSystem.GrindAttemptEvent>(OnGrindAttempt);
        SubscribeLocalEvent<RevenantStasisComponent, TransformSpeakerNameEvent>(OnTransformName);
        SubscribeLocalEvent<RevenantStasisComponent, AfterInteractUsingEvent>(OnBibleInteract, before: [typeof(BibleSystem)]);
        SubscribeLocalEvent<RevenantStasisComponent, ExorciseRevenantDoAfterEvent>(OnExorcise);
    }

    private void OnStartup(EntityUid uid, RevenantStasisComponent component, ComponentStartup args)
    {
        EnsureComp<SpeechComponent>(uid);
        _status.TryAddStatusEffectDuration(uid, RevenantStatusEffects.Stasis, component.StasisDuration);

        var mover = EnsureComp<InputMoverComponent>(uid);
        mover.CanMove = false;
        Dirty(uid, mover);

        var speech = EnsureComp<SpeechComponent>(uid);
        speech.SpeechVerb = "Ghost";
        Dirty(uid, speech);

        if (TryComp<GhostRoleComponent>(uid, out var ghostRole))
            _ghostRoles.UnregisterGhostRole((uid, ghostRole!));
    }

    private void OnTransformName(EntityUid uid, RevenantStasisComponent comp, TransformSpeakerNameEvent args)
    {
        args.VoiceName = Name(comp.Revenant);
        args.SpeechVerb = "Ghost";
    }

    private void OnShutdown(EntityUid uid, RevenantStasisComponent component, ComponentShutdown args)
    {
        if (_status.HasStatusEffect(uid, RevenantStatusEffects.Stasis))
        {
            if (_mind.TryGetMind(uid, out var mindId, out _))
                _mind.TransferTo(mindId, null);

            QueueDel(component.Revenant);
        }
    }

    private void OnStasisStatusRemoved(Entity<RevenantStasisStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (!TryComp<RevenantStasisComponent>(args.Target, out var stasis))
            return;

        _transformSystem.SetCoordinates(stasis.Revenant, Transform(args.Target).Coordinates);
        _transformSystem.AttachToGridOrMap(stasis.Revenant);
        _meta.SetEntityPaused(stasis.Revenant, false);

        if (_mind.TryGetMind(args.Target, out var mindId, out _))
            _mind.TransferTo(mindId, stasis.Revenant);

        QueueDel(args.Target);
    }

    private void OnExamine(Entity<RevenantStasisComponent> entity, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("revenant-stasis-regenerating"));
    }

    private void OnCrafted(EntityUid uid, RevenantStasisComponent comp, ConstructionConsumedObjectEvent args)
    {
        var voice = EnsureComp<VoiceOverrideComponent>(args.New);
        voice.SpeechVerbOverride = "Ghost";
        voice.NameOverride = Name(comp.Revenant);

        if (_mind.TryGetMind(uid, out var mindId, out _))
            _mind.TransferTo(mindId, args.New);
    }

    private void OnGrindAttempt(EntityUid uid, RevenantStasisComponent comp, ReagentGrinderSystem.GrindAttemptEvent args)
    {
        if (!TryComp<RevenantComponent>(comp.Revenant, out var revenant) || !revenant.GrindingRequiresSalt)
            return;

        foreach (var reagent in args.Reagents)
        {
            if (_tags.HasAnyTag(reagent, "Salt", "Holy"))
                return;
        }

        _explosion.QueueExplosion(args.Grinder.Owner, "Default", 7.5f, 4f, 2f);
        args.Cancel();
    }

    private void OnAttemptDirection(EntityUid uid, RevenantStasisComponent comp, ChangeDirectionAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnBibleInteract(EntityUid uid, RevenantStasisComponent comp, AfterInteractUsingEvent args)
    {
        if (args.Handled || args.Target == null)
            return;

        if (!HasComp<BibleComponent>(args.Used))
            return;

        var target = args.Target.Value;
        var bible = args.Used;
        var user = args.User;

        if (!TryComp<RevenantStasisComponent>(target, out _))
            return;

        if (TryComp<RevenantComponent>(comp.Revenant, out var revenant)
            && revenant.ExorcismRequiresBibleUser
            && !HasComp<BibleUserComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("revenant-exorcise-fail", ("bible", bible)), user, user);
            return;
        }

        var doAfterEventArgs = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(10),
            new ExorciseRevenantDoAfterEvent(), target, target, bible)
        {
            BreakOnMove = true,
            BreakOnWeightlessMove = false,
            BreakOnDamage = true,
            NeedHand = true,
            DistanceThreshold = 1f
        };

        if (!_doAfter.TryStartDoAfter(doAfterEventArgs))
            return;

        args.Handled = true;

        _popup.PopupEntity(
            Loc.GetString("revenant-exorcise-begin-user", ("bible", bible), ("user", user), ("revenant", comp.Revenant)),
            user, user);
        _popup.PopupEntity(
            Loc.GetString("revenant-exorcise-begin-target", ("bible", bible), ("user", user), ("revenant", comp.Revenant)),
            target, target, PopupType.MediumCaution);
        _popup.PopupEntity(
            Loc.GetString("revenant-exorcise-begin-other", ("bible", bible), ("user", user), ("revenant", comp.Revenant)),
            target, Filter.Pvs(target).RemovePlayersByAttachedEntity(user, target), true);
    }

    private void OnExorcise(EntityUid uid, RevenantStasisComponent comp, ExorciseRevenantDoAfterEvent args)
    {
        if (args.Cancelled || args.Target == null || args.Used == null)
            return;

        _popup.PopupEntity(
            Loc.GetString("revenant-exorcise-success",
                ("bible", args.Used.Value), ("user", args.User), ("revenant", comp.Revenant)),
            args.Target.Value);

        RemComp<RevenantStasisComponent>(args.Target.Value);
    }
}
