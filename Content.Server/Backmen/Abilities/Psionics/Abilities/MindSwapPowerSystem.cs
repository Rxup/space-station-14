using Content.Server.Backmen.Psionics;
using Content.Shared.Actions;
using Content.Shared.Speech;
using Content.Shared.Stealth.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.Damage;
using Content.Server.Mind;
using Content.Shared.Mobs.Systems;
using Content.Server.Popups;
using Content.Server.GameTicking;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Blob;
using Content.Shared.Backmen.Blob.Components;
using Content.Shared.Backmen.Psionics.Events;
using Content.Shared.Mind.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared.NPC;
using Content.Shared.SSDIndicator;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Abilities.Psionics;

public sealed class MindSwapPowerSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly SharedPsionicAbilitiesSystem _psionics = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    #if !DEBUG
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    #endif

    private ISawmill _logger = default!;

    public override void Initialize()
    {
        base.Initialize();
        _logger = Logger.GetSawmill("mindswap");
        SubscribeLocalEvent<MindSwapPowerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MindSwapPowerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MindSwapPowerActionEvent>(OnPowerUsed);
        SubscribeLocalEvent<MindSwappedComponent, MindSwapPowerReturnActionEvent>(OnPowerReturned);
        SubscribeLocalEvent<MindSwappedComponent, DispelledEvent>(OnDispelled);
        SubscribeLocalEvent<MindSwappedComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<GhostAttemptHandleEvent>(OnGhostAttempt);
        //
        SubscribeLocalEvent<MindSwappedComponent, ComponentInit>(OnSwapInit);
    }

    [ValidatePrototypeId<EntityPrototype>] private const string ActionMindSwap = "ActionMindSwap";
    [ValidatePrototypeId<EntityPrototype>] private const string ActionMindSwapReturn = "ActionMindSwapReturn";

    private void OnInit(EntityUid uid, MindSwapPowerComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.MindSwapPowerAction, ActionMindSwap);

    #if !DEBUG
         if (_actions.TryGetActionData(component.MindSwapPowerAction, out var action) && action?.UseDelay != null)
            _actions.SetCooldown(component.MindSwapPowerAction, _gameTiming.CurTime,
                _gameTiming.CurTime + (TimeSpan)  action?.UseDelay!);
    #endif
        if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
            psionic.PsionicAbility = component.MindSwapPowerAction;


    }

    private void OnShutdown(EntityUid uid, MindSwapPowerComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.MindSwapPowerAction);
    }

    private void OnPowerUsed(MindSwapPowerActionEvent args)
    {
        if (!(TryComp<DamageableComponent>(args.Target, out var damageable) && damageable.DamageContainerID == "Biological"))
            return;

        if (HasComp<PsionicInsulationComponent>(args.Target))
            return;

        _psionics.LogPowerUsed(args.Performer, "mind swap");
        if (Swap(args.Performer, args.Target))
        {
            GetTrapped(args.Performer);
        }
        args.Handled = true;
    }

    private void OnPowerReturned(EntityUid uid, MindSwappedComponent component, MindSwapPowerReturnActionEvent args)
    {
        if (HasComp<PsionicInsulationComponent>(component.OriginalEntity) || HasComp<PsionicInsulationComponent>(uid))
            return;

        if (HasComp<MobStateComponent>(uid) && !_mobStateSystem.IsAlive(uid))
            return;

        // How do we get trapped?
        // 1. Original target doesn't exist
        if (!component.OriginalEntity.IsValid() || Deleted(component.OriginalEntity))
        {
            GetTrapped(uid);
            return;
        }
        // 1. Original target is no longer mindswapped
        if (!TryComp<MindSwappedComponent>(component.OriginalEntity, out var targetMindSwap))
        {
            GetTrapped(uid);
            return;
        }

        // 2. Target has undergone a different mind swap
        if (targetMindSwap.OriginalEntity != uid)
        {
            GetTrapped(uid);
            return;
        }

        // 3. Target is dead
        if (HasComp<MobStateComponent>(component.OriginalEntity) && _mobStateSystem.IsDead(component.OriginalEntity))
        {
            GetTrapped(uid);
            return;
        }

        Swap(uid, component.OriginalEntity, true);
    }

    private void OnDispelled(EntityUid uid, MindSwappedComponent component, DispelledEvent args)
    {
        Swap(uid, component.OriginalEntity, true);
        args.Handled = true;
    }

    private void OnMobStateChanged(EntityUid uid, MindSwappedComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            RemComp<MindSwappedComponent>(uid);
            if (!TerminatingOrDeleted(component.OriginalEntity) && HasComp<MindSwappedComponent>(component.OriginalEntity))
            {
                RemCompDeferred<MindSwappedComponent>(component.OriginalEntity);
                GetTrapped(component.OriginalEntity);
            }
        }

    }

    private void OnGhostAttempt(GhostAttemptHandleEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<MindSwappedComponent>(args.Mind.CurrentEntity))
            return;

        if (!args.ViaCommand)
            return;

        args.Result = false;
        args.Handled = true;
    }

    private void OnSwapInit(EntityUid uid, MindSwappedComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.MindSwapReturn, ActionMindSwapReturn);
    }

    public bool Swap(EntityUid performer, EntityUid target, bool end = false)
    {
        if (performer == target)
        {
            return false;
        }
        if (end && (!HasComp<MindSwappedComponent>(performer) || !HasComp<MindSwappedComponent>(target)))
        {
            return false;
        }
        if (!end)
        {
            if (HasComp<MindSwappedComponent>(performer))
            {
                _popupSystem.PopupCursor("Ошибка! Вы уже в другом теле!", performer);
                return false; // Повторный свап!? TODO: chain swap, in current mode broken chained in no return (has no mind error)
            }

            if (HasComp<MindSwappedComponent>(target))
            {
                _popupSystem.PopupCursor("Ошибка! Ваша цель уже в другом теле!", performer);
                return false; // Повторный свап!? TODO: chain swap, in current mode broken chained in no return (has no mind error)
            }
/*
            if (HasComp<ActiveNPCComponent>(performer) || HasComp<ActiveNPCComponent>(target))
            {
                _popupSystem.PopupCursor("Ошибка! Ваша цель в ссд!", performer);
                return false;
            }
*/
            if (HasComp<MindShieldComponent>(target))
            {
                _popupSystem.PopupCursor("Ошибка! Ваша цель имеет защиту разума!", performer);
                return false;
            }

            if (HasComp<BlobCarrierComponent>(target) || HasComp<BlobCarrierComponent>(performer))
            {
                _popupSystem.PopupCursor("Ошибка! Ваша цель не стабильна!", performer);
                return false;
            }
        }
        // This is here to prevent missing MindContainerComponent Resolve errors.
        var a = _mindSystem.TryGetMind(performer, out var performerMindId, out var performerMind);
        var b = _mindSystem.TryGetMind(target, out var targetMindId, out var targetMind);


        _logger.Info($"swap performer: {ToPrettyString(performer):Entity} target: {ToPrettyString(target):Entity}");

        ICommonSession? performerSession = null;
        ICommonSession? targetSession = null;

        if (a)
        {
            performerSession = performerMind!.Session;
            _mindSystem.TransferTo(performerMindId, null, true);
        }

        if (b)
        {
            targetSession = targetMind!.Session;
            _mindSystem.TransferTo(targetMindId, null, true);
        }

        // Do the transfer.
        if (a)
        {
            RemComp<ActorComponent>(target);
            RemComp<MindContainerComponent>(target);
            //_mindSystem.SetUserId(performerMindId, performerMind!.UserId, performerMind);
            var isSsd = performerSession == null;

            _mindSystem.TransferTo(performerMindId, target, true, false);
            Timer.Spawn(1_000, () =>
            {
                if (!target.IsValid() || !TryComp<SSDIndicatorComponent>(target, out var ssd))
                    return;
                ssd.IsSSD = isSsd;
                Dirty(target,ssd);
            });

        }


        if (b)
        {

            RemComp<ActorComponent>(performer);
            RemComp<MindContainerComponent>(performer);
            //_mindSystem.SetUserId(targetMindId, targetMind!.UserId, targetMind);
            var isSsd = targetSession == null;
            _mindSystem.TransferTo(targetMindId, performer, true, false);
            Timer.Spawn(1_000, () =>
            {
                if (!performer.IsValid() || !TryComp<SSDIndicatorComponent>(performer, out var ssd))
                    return;
                ssd.IsSSD = isSsd;
                Dirty(performer,ssd);
            });

        }

        if (end)
        {
            if (TryComp<MindSwappedComponent>(performer, out var mindSwapCompP))
            {
                _actions.RemoveAction(performer,  mindSwapCompP.MindSwapReturn);
            }
            if (TryComp<MindSwappedComponent>(target, out var mindSwapCompT))
            {
                _actions.RemoveAction(target, mindSwapCompT.MindSwapReturn);
            }

            RemComp<MindSwappedComponent>(performer);
            RemComp<MindSwappedComponent>(target);

            return true;
        }

        var perfComp = EnsureComp<MindSwappedComponent>(performer);
        var targetComp = EnsureComp<MindSwappedComponent>(target);

        perfComp.OriginalEntity = target;
        perfComp.OriginalMindId = targetMindId;
        targetComp.OriginalEntity = performer;
        targetComp.OriginalMindId = performerMindId;

        return true;
    }

    public void GetTrapped(EntityUid uid)
    {
        _popupSystem.PopupEntity(Loc.GetString("mindswap-trapped"), uid, uid, Shared.Popups.PopupType.LargeCaution);
        if (TryComp<MindSwappedComponent>(uid, out var mindSwappedComp))
        {
            _actions.RemoveAction(uid, mindSwappedComp.MindSwapReturn);
        }

        if (HasComp<TelegnosticProjectionComponent>(uid))
        {
            RemComp<PsionicallyInvisibleComponent>(uid);
            RemComp<StealthComponent>(uid);
            EnsureComp<SpeechComponent>(uid);
            EnsureComp<DispellableComponent>(uid);
            _metaDataSystem.SetEntityName(uid,Loc.GetString("telegnostic-trapped-entity-name"));
            _metaDataSystem.SetEntityDescription(uid, Loc.GetString("telegnostic-trapped-entity-desc"));
        }
    }
}
