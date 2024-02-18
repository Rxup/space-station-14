using Content.Server.Mind;
using Content.Server.Nuke;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Roles;
using Content.Server.Storage.Components;
using Content.Shared.Actions;
using Content.Shared.Backmen.EntityHealthBar;
using Content.Shared.Backmen.StationAI;
using Content.Shared.Backmen.StationAI.Events;
using Content.Shared.Backmen.StationAI.UI;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Nuke;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Player;

namespace Content.Server.Backmen.StationAI.Systems;

public sealed class StationAISystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    [Dependency] private readonly PopupSystem _popup = default!;

    [Dependency] private readonly NukeSystem _nuke = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly MindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAIComponent, MapInitEvent>(OnStartup);
        SubscribeLocalEvent<StationAIComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StationAIComponent, EntityTerminatingEvent>(OnTerminated);

        SubscribeLocalEvent<StationAIComponent, InteractionAttemptEvent>(CanInteraction);

        SubscribeLocalEvent<AIHealthOverlayEvent>(OnHealthOverlayEvent);
        SubscribeLocalEvent<StationAIComponent, ToggleArmNukeEvent>(OnToggleNuke);
        SubscribeLocalEvent<StationAIComponent, PlayerAttachedEvent>(OnRoleUpdateIfNeed);
    }

    [ValidatePrototypeId<JobPrototype>]
    private const string SAIJob = "SAI";

    private void OnRoleUpdateIfNeed(Entity<StationAIComponent> ent, ref PlayerAttachedEvent args)
    {
        if (!_mind.TryGetMind(args.Player, out var mindId, out var mind))
        {
            return;
        }

        var job = CompOrNull<JobComponent>(mindId);
        if (job != null && job.Prototype == SAIJob)
        {
            return;
        }

        if (job != null)
        {
            _role.MindRemoveRole<JobComponent>(mindId);
        }

        _role.MindAddRole(mindId, new JobComponent
        {
            Prototype = SAIJob
        }, mind);
    }

    private void OnToggleNuke(Entity<StationAIComponent> ent, ref ToggleArmNukeEvent args)
    {
        if (!TryComp<NukeComponent>(ent, out var nuke))
        {
            return;
        }

        if (nuke.Status == NukeStatus.COOLDOWN)
        {
            _popup.PopupCursor("На перезарядке!",ent);
            return;
        }

        if (nuke.DiskSlot.Item != null)
        {
            _popup.PopupCursor("Невозможно управлять бомбой при вставленном диске!",ent);
            return;
        }

        args.Handled = true;
        if (nuke.Status == NukeStatus.ARMED)
        {
            _nuke.DisarmBomb(ent, nuke);
        }
        else
        {
            _nuke.ArmBomb(ent, nuke);
        }
    }

    private void CanInteraction(Entity<StationAIComponent> ent, ref InteractionAttemptEvent args)
    {
        var core = ent;
        if (TryComp<AIEyeComponent>(ent, out var eye))
        {
            if (eye.AiCore == null)
            {
                QueueDel(ent);
                args.Cancel();
                return;
            }
            core = eye.AiCore.Value;
        }
        if (!core.Owner.Valid)
        {
            args.Cancel();
            return;
        }

        if (args.Target != null && Transform(core).GridUid != Transform(args.Target.Value).GridUid)
        {
            args.Cancel();
            return;
        }

        if (!TryComp<ApcPowerReceiverComponent>(core, out var power))
        {
            args.Cancel();
            return;
        }

        if (power is { NeedsPower: true, Powered: false })
        {
            args.Cancel();
            return;
        }

        if (HasComp<ItemComponent>(args.Target))
        {
            args.Cancel();
            return;
        }

        if (HasComp<EntityStorageComponent>(args.Target))
        {
            args.Cancel();
            return;
        }

        if (TryComp<ApcPowerReceiverComponent>(args.Target, out var targetPower) && targetPower.NeedsPower && !targetPower.Powered)
        {
            args.Cancel();
            return;
        }
    }

    private void OnTerminated(Entity<StationAIComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!ent.Comp.ActiveEye.IsValid())
        {
            return;
        }
        QueueDel(ent.Comp.ActiveEye);
    }

    private void OnStartup(EntityUid uid, StationAIComponent component, MapInitEvent args)
    {
        _actions.AddAction(uid, ref component.ActionId, component.Action);
        _hands.AddHand(uid,"SAI",HandLocation.Middle);

        if (!HasComp<AIEyeComponent>(uid) && TryComp<NukeComponent>(uid, out var nuke))
        {
            (nuke as dynamic).Status = NukeStatus.COOLDOWN;
            (nuke as dynamic).CooldownTime = 1;
            _actions.AddAction(uid, ref component.NukeToggleId, component.NukeToggle);
        }
    }

    private void OnShutdown(EntityUid uid, StationAIComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.ActionId);
        _actions.RemoveAction(uid, component.NukeToggleId);
    }

    private void OnHealthOverlayEvent(AIHealthOverlayEvent args)
    {
        if (HasComp<BkmShowHealthBarsComponent>(args.Performer))
        {
            RemCompDeferred<BkmShowHealthBarsComponent>(args.Performer);
        }
        else
        {
            var comp = EnsureComp<BkmShowHealthBarsComponent>(args.Performer);
            comp.DamageContainers.Clear();
            comp.DamageContainers.Add("Biological");
            comp.DamageContainers.Add("HalfSpirit");
            Dirty(args.Performer, comp);
        }
        args.Handled = true;
    }
}
