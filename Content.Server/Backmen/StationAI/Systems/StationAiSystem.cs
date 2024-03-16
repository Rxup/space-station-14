using System.Linq;
using Content.Server.Administration;
using Content.Server.Mind;
using Content.Server.Nuke;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Roles;
using Content.Server.Storage.Components;
using Content.Shared.Actions;
using Content.Shared.Administration;
using Content.Shared.Backmen.EntityHealthBar;
using Content.Shared.Backmen.StationAI;
using Content.Shared.Backmen.StationAI.Events;
using Content.Shared.Backmen.StationAI.UI;
using Content.Shared.Destructible;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nuke;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Silicons.Laws;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Backmen.StationAI.Systems;

public sealed class StationAiSystem : SharedStationAISystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    [Dependency] private readonly PopupSystem _popup = default!;

    [Dependency] private readonly NukeSystem _nuke = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IRobustRandom _robust = default!;
    [Dependency] private readonly IConsoleHost _console = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAIComponent, MapInitEvent>(OnStartup);
        SubscribeLocalEvent<StationAIComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StationAIComponent, EntityTerminatingEvent>(OnTerminated);

        SubscribeLocalEvent<StationAIComponent, InteractionAttemptEvent>(CanInteraction);

        SubscribeLocalEvent<StationAIComponent, AIHealthOverlayEvent>(OnHealthOverlayEvent);
        SubscribeLocalEvent<StationAIComponent, ToggleArmNukeEvent>(OnToggleNuke);
        SubscribeLocalEvent<StationAIComponent, PlayerAttachedEvent>(OnRoleUpdateIfNeed);

        //SubscribeLocalEvent<StationAIComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<StationAIComponent, MobStateChangedEvent>(OnMobStateChanged);

        _console.RegisterCommand("aiset_screen", AiSetScreen, AiSetScreenCompletion);
        _console.RegisterCommand("aiset_law", AiSetLaw, AiSetLawCompletion);
    }

    #region aiset_law

    private CompletionResult AiSetLawCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(
                StationAiComponents(args[^1]),
                "ИИ"),
            2 => CompletionResult.FromHintOptions(
                CompletionHelper.PrototypeIDs<SiliconLawsetPrototype>(),
                "Закон"),
            _ => CompletionResult.Empty
        };
    }

    [AdminCommand(AdminFlags.Fun)]
    private void AiSetLaw(IConsoleShell shell, string argstr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var netEntity) || !TryGetEntity(netEntity, out var ent))
        {
            shell.WriteError(Loc.GetString("shell-entity-uid-must-be-number"));
            return;
        }

        if (!TryComp<StationAIComponent>(ent, out var stationAiComponent) ||
            HasComp<AIEyeComponent>(ent) ||
            !_prototypeManager.TryIndex<SiliconLawsetPrototype>(args[1], out var lawsetPrototype)
            )
            return;

        stationAiComponent.SelectedLaw = lawsetPrototype;

        if (stationAiComponent.ActiveEye.IsValid() && !TerminatingOrDeleted(stationAiComponent.ActiveEye))
            EnsureComp<StationAIComponent>(stationAiComponent.ActiveEye).SelectedLaw = lawsetPrototype;

        shell.WriteLine("OK!");
    }

    #endregion

    #region aiset_screen

    private CompletionResult AiSetScreenCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(
                StationAiComponents(args[^1]),
                "ИИ"),
            2 => CompletionResult.FromHint("Экран ии"),
            _ => CompletionResult.Empty
        };
    }
    [AdminCommand(AdminFlags.Fun)]
    private void AiSetScreen(IConsoleShell shell, string argstr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var netEntity) || !TryGetEntity(netEntity, out var ent))
        {
            shell.WriteError(Loc.GetString("shell-entity-uid-must-be-number"));
            return;
        }

        if (!TryComp<StationAIComponent>(ent, out var stationAiComponent) ||
            HasComp<AIEyeComponent>(ent)
           )
            return;

        if (args.Length != 2 || !stationAiComponent.Layers.ContainsKey(args[1]))
        {
            shell.WriteError("Текущий экран: "+stationAiComponent.SelectedLayer);
            shell.WriteError("Доступные экраны: "+string.Join(", ",stationAiComponent.Layers.Keys));
            return;
        }

        stationAiComponent.SelectedLayer = args[1];
        Dirty(ent.Value,stationAiComponent);

        if (stationAiComponent.ActiveEye.IsValid() && !TerminatingOrDeleted(stationAiComponent.ActiveEye))
        {
            var comp = EnsureComp<StationAIComponent>(stationAiComponent.ActiveEye);
            comp.SelectedLayer = args[1];
            Dirty(stationAiComponent.ActiveEye,comp);
        }

        shell.WriteLine("OK!");
    }

    #endregion


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
        if (_mobState.IsDead(ent))
            return;

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
        if (_mobState.IsDead(core))
            return;

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

        if (HasComp<NukeComponent>(args.Target))
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
        if (component.Layers.Count == 0)
        {
            return;
        }
        _actions.AddAction(uid, ref component.ActionId, component.Action);
        _hands.AddHand(uid,"SAI",HandLocation.Middle);

        if (!HasComp<AIEyeComponent>(uid))
        {
            component.SelectedLayer = _robust.Pick(component.Layers.Keys);
            Dirty(uid,component);

            _appearance.SetData(uid, AiVisuals.Dead, false);
            _appearance.SetData(uid, AiVisuals.InEye, false);
            if (TryComp<NukeComponent>(uid, out var nuke))
            {
                (nuke as dynamic).Status = NukeStatus.COOLDOWN;
                (nuke as dynamic).CooldownTime = 1;
                _actions.AddAction(uid, ref component.NukeToggleId, component.NukeToggle);
            }
        }
    }

    private void OnShutdown(EntityUid uid, StationAIComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.ActionId);
        _actions.RemoveAction(uid, component.NukeToggleId);
    }

    private void OnHealthOverlayEvent(Entity<StationAIComponent> ent, ref AIHealthOverlayEvent args)
    {
        var core = ent.Owner;
        if (TryComp<AIEyeComponent>(ent, out var eye))
        {
            core = eye.AiCore?.Owner ?? EntityUid.Invalid;
        }

        if (!core.Valid)
            return;
        if (_mobState.IsDead(core))
            return;

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

    private static readonly SoundSpecifier AiDeath =
        new SoundPathSpecifier("/Audio/Machines/AI/borg_death.ogg");
    private void OnMobStateChanged(EntityUid uid, StationAIComponent component, MobStateChangedEvent args)
    {
        if (HasComp<AIEyeComponent>(uid))
            return;

        if (args.OldMobState != MobState.Dead && args.NewMobState == MobState.Dead)
        {
            SetAsDead((uid,component),true);
        }
        else if (args.OldMobState != MobState.Alive && args.NewMobState == MobState.Alive)
        {
            SetAsDead((uid,component),false);
        }

    }

    private void SetAsDead(Entity<StationAIComponent> ent,bool state)
    {
        if (state)
        {
            if (ent.Comp.ActiveEye.IsValid())
            {
                QueueDel(ent.Comp.ActiveEye);
            }

            _audioSystem.PlayPvs(AiDeath, ent);
            _appearance.SetData(ent, AiVisuals.Dead, true);
        }
        else
        {
            _appearance.SetData(ent, AiVisuals.Dead, false);
            _mobState.ChangeMobState(ent, MobState.Alive);
        }
    }
}
