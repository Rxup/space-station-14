using Content.Server.Bible;
using Content.Server.Bible.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chat;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Kitchen.Components;
using Content.Shared.Kitchen.EntitySystems;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared._Impstation.Revenant;
using Content.Shared._Impstation.Revenant.Components;
using Content.Shared.Revenant;
using Content.Shared.Revenant.Components;
using Content.Shared.Follower;
using Content.Shared.Follower.Components;
using Content.Shared.Speech;
using Content.Shared.StatusEffectNew;
using Content.Shared.Tag;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

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
    [Dependency] private FollowerSystem _followerSystem = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;

    private static readonly ProtoId<TagPrototype> Salt = "Salt";
    private static readonly ProtoId<TagPrototype> EctoplasmTag = "Ectoplasm";
    private static readonly ProtoId<ReagentPrototype> TableSalt = "TableSalt";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevenantStasisComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<RevenantStasisComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<RevenantStasisStatusEffectComponent, StatusEffectRemovedEvent>(OnStasisStatusRemoved);
        SubscribeLocalEvent<RevenantStasisComponent, ChangeDirectionAttemptEvent>(OnAttemptDirection);
        SubscribeLocalEvent<RevenantStasisComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<RevenantStasisComponent, GrindAttemptEvent>(OnGrindAttempt);
        SubscribeLocalEvent<RevenantStasisComponent, TransformSpeakerNameEvent>(OnTransformName);
        SubscribeLocalEvent<RevenantStasisComponent, AfterInteractUsingEvent>(OnBibleInteract,
            before: [typeof(BibleSystem)]);
        SubscribeLocalEvent<RevenantStasisComponent, ExorciseRevenantDoAfterEvent>(OnExorcise);
    }

    private void OnStartup(EntityUid uid, RevenantStasisComponent component, ComponentStartup args)
    {
        // Block scoop/spike kill bypass (inherited from Ash).
        RemComp<ScoopableSolutionComponent>(uid);
        RemComp<SolutionSpikerComponent>(uid);

        // Stasis ectoplasm is not craft material — only bible or salt grind can end a revenant.
        _tags.RemoveTag(uid, EctoplasmTag);

        if (TryComp<FollowedComponent>(component.Revenant, out var followed))
        {
            foreach (var follower in followed.Following)
            {
                _followerSystem.StartFollowingEntity(follower, uid);
            }
        }

        EnsureComp<SpeechComponent>(uid);
        _status.TryAddStatusEffectDuration(uid, RevenantStatusEffects.Stasis, component.StasisDuration);

        var mover = EnsureComp<InputMoverComponent>(uid);
        mover.CanMove = false;
        Dirty(uid, mover);

        var speech = EnsureComp<SpeechComponent>(uid);
        speech.SpeechVerb = "Ghost";
        Dirty(uid, speech);

        if (TryComp<GhostRoleComponent>(uid, out var ghostRole))
            _ghostRoles.UnregisterGhostRole((uid, ghostRole));
    }

    private void OnTransformName(EntityUid uid, RevenantStasisComponent comp, TransformSpeakerNameEvent args)
    {
        args.VoiceName = Name(comp.Revenant);
        args.SpeechVerb = "Ghost";
    }

    private void OnShutdown(EntityUid uid, RevenantStasisComponent component, ComponentShutdown args)
    {
        // Already revived (failed grind / blender broke) — ectoplasm is just being cleaned up.
        if (component.Revived)
            return;

        // Intentional kill: salt grind or bible exorcism only.
        if (component.PermanentlyDestroyed)
        {
            if (_mind.TryGetMind(uid, out var mindId, out _))
                _mind.TransferTo(mindId, null);

            if (!TerminatingOrDeleted(component.Revenant))
                QueueDel(component.Revenant);
            return;
        }

        // Accidental ectoplasm deletion while still regenerating (e.g. blender exploded).
        if (_status.HasStatusEffect(uid, RevenantStatusEffects.Stasis)
            || (Exists(component.Revenant) && MetaData(component.Revenant).EntityPaused))
        {
            TryReviveFromStasis(uid, component);
        }
    }

    private void OnStasisStatusRemoved(Entity<RevenantStasisStatusEffectComponent> ent,
        ref StatusEffectRemovedEvent args)
    {
        if (!TryComp<RevenantStasisComponent>(args.Target, out var stasis) || TerminatingOrDeleted(args.Target))
            return;

        if (stasis.Revived || stasis.PermanentlyDestroyed)
            return;

        TryReviveFromStasis(args.Target, stasis);
        QueueDel(args.Target);
    }

    /// <summary>
    /// Returns the paused revenant to the ectoplasm's location and moves the mind back.
    /// </summary>
    private void TryReviveFromStasis(EntityUid ectoplasm, RevenantStasisComponent stasis)
    {
        if (stasis.Revived || TerminatingOrDeleted(stasis.Revenant))
            return;

        stasis.Revived = true;

        var coords = Transform(ectoplasm).Coordinates;
        _transformSystem.SetCoordinates(stasis.Revenant, coords);
        _transformSystem.AttachToGridOrMap(stasis.Revenant);
        _meta.SetEntityPaused(stasis.Revenant, false);

        if (_mind.TryGetMind(ectoplasm, out var mindId, out _))
            _mind.TransferTo(mindId, stasis.Revenant);

        if (TryComp<FollowedComponent>(ectoplasm, out var followed))
        {
            foreach (var follower in new List<EntityUid>(followed.Following))
            {
                _followerSystem.StartFollowingEntity(follower, stasis.Revenant);
            }
        }

        _popup.PopupEntity(Loc.GetString("revenant-stasis-regenerating"), stasis.Revenant, PopupType.Medium);
    }

    private void OnExamine(Entity<RevenantStasisComponent> entity, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("revenant-stasis-regenerating"));
    }

    private void OnGrindAttempt(EntityUid uid, RevenantStasisComponent comp, GrindAttemptEvent args)
    {
        var hasSalt = HasGrindSalt(args);

        var requiresSalt = !TryComp<RevenantComponent>(comp.Revenant, out var revenant)
            || revenant.GrindingRequiresSalt;

        // Salt (or salt not required): allow grind → DestroyEntity → permanent death.
        if (hasSalt || !requiresSalt)
        {
            comp.PermanentlyDestroyed = true;
            return;
        }

        // No salt: cancel grind, blow up the blender, revive the revenant.
        args.Cancel();
        _explosion.QueueExplosion(args.Grinder, "Default", 7.5f, 4f, 2f);
        TryReviveFromStasis(uid, comp);
        QueueDel(uid);
    }

    /// <summary>
    /// Salt ore / salt packet (tag), TableSalt in hopper items, or TableSalt already in the grinder beaker.
    /// </summary>
    private bool HasGrindSalt(GrindAttemptEvent args)
    {
        foreach (var content in args.Contents)
        {
            if (_tags.HasTag(content, Salt) || EntityContainsTableSalt(content))
                return true;
        }

        var beaker = _itemSlots.GetItemOrNull(args.Grinder, ReagentGrinderComponent.BeakerSlotId);
        return beaker != null && EntityContainsTableSalt(beaker.Value);
    }

    private bool EntityContainsTableSalt(EntityUid uid)
    {
        foreach (var (_, soln) in _solution.EnumerateSolutions(uid))
        {
            if (soln.Comp.Solution.ContainsPrototype(TableSalt))
                return true;
        }

        return false;
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

        var doAfterEventArgs = new DoAfterArgs(EntityManager,
            user,
            TimeSpan.FromSeconds(10),
            new ExorciseRevenantDoAfterEvent(),
            target,
            target,
            bible)
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
            Loc.GetString("revenant-exorcise-begin-user",
                ("bible", bible),
                ("user", user),
                ("revenant", comp.Revenant)),
            user,
            user);
        _popup.PopupEntity(
            Loc.GetString("revenant-exorcise-begin-target",
                ("bible", bible),
                ("user", user),
                ("revenant", comp.Revenant)),
            target,
            target,
            PopupType.MediumCaution);
        _popup.PopupEntity(
            Loc.GetString("revenant-exorcise-begin-other",
                ("bible", bible),
                ("user", user),
                ("revenant", comp.Revenant)),
            target,
            Filter.Pvs(target).RemovePlayersByAttachedEntity(user, target),
            true);
    }

    private void OnExorcise(EntityUid uid, RevenantStasisComponent comp, ExorciseRevenantDoAfterEvent args)
    {
        if (args.Cancelled || args.Target == null || args.Used == null)
            return;

        _popup.PopupEntity(
            Loc.GetString("revenant-exorcise-success",
                ("bible", args.Used.Value),
                ("user", args.User),
                ("revenant", comp.Revenant)),
            args.Target.Value);

        comp.PermanentlyDestroyed = true;
        RemComp<RevenantStasisComponent>(args.Target.Value);
    }
}
