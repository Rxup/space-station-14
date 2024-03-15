using Content.Server.Backmen.StationAI.Systems;
using Content.Server.Mind;
using Content.Server.Power.Components;
using Content.Server.Speech.Components;
using Content.Server.SurveillanceCamera;
using Content.Shared.Actions;
using Content.Shared.Backmen.StationAI;
using Content.Shared.Backmen.StationAI.UI;
using Content.Shared.Eye;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.Prototypes;
using Content.Shared.Mobs.Systems;
using Content.Shared.Random.Helpers;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server.Backmen.StationAI;

public sealed class AIEyePowerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;

    [Dependency] private readonly VisibilitySystem _visibilitySystem = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly SharedEyeSystem _sharedEyeSystem = default!;

    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

    [Dependency] private readonly AICameraSystem _cameraSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AIEyePowerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<AIEyePowerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AIEyePowerComponent, AIEyePowerActionEvent>(OnPowerUsed);

        SubscribeLocalEvent<AIEyeComponent, AIEyePowerReturnActionEvent>(OnPowerReturnUsed);
        SubscribeLocalEvent<AIEyeComponent, ComponentShutdown>(OnEyeRemove);

        SubscribeLocalEvent<AIEyeComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AIEyeComponent, MindRemovedMessage>(OnMindRemoved);
        SubscribeLocalEvent<AIEyeComponent, MindUnvisitedMessage>(OnMindRemoved2);

        SubscribeLocalEvent<StationAIComponent, GetSiliconLawsEvent>(OnGetLaws);

        SubscribeLocalEvent<StationAIComponent, PowerChangedEvent>(OnPowerChange);

        SubscribeLocalEvent<AIEyeComponent, AIEyeCampActionEvent>(OnOpenUiCams);
    }

    private void OnOpenUiCams(Entity<AIEyeComponent> ent, ref AIEyeCampActionEvent args)
    {
        if (!TryComp<ActorComponent>(ent, out var actorComponent))
        {
            return;
        }

        _uiSystem.TryToggleUi(ent, AICameraListUiKey.Key, actorComponent.PlayerSession);
    }

    private void OnEyeRemove(Entity<AIEyeComponent> ent, ref ComponentShutdown args)
    {
        _uiSystem.TryCloseAll(ent, AICameraListUiKey.Key);
        _cameraSystem.RemoveActiveCamera(ent);
    }

    private void OnPowerChange(EntityUid uid, StationAIComponent component, ref PowerChangedEvent args)
    {
        if (HasComp<AIEyeComponent>(uid) || TerminatingOrDeleted(uid))
        {
            return;
        }

        foreach (var (actionId,action) in _actions.GetActions(uid))
        {
            _actions.SetEnabled(actionId, args.Powered);
        }

        if (!args.Powered && component.ActiveEye.IsValid())
        {
            QueueDel(component.ActiveEye);
            component.ActiveEye = EntityUid.Invalid;
        }

        if (!args.Powered)
        {
            EnsureComp<ReplacementAccentComponent>(uid).Accent = "dwarf";
            _uiSystem.TryCloseAll(uid);
        }
        else
        {
            RemCompDeferred<ReplacementAccentComponent>(uid);
        }
    }

    [ValidatePrototypeId<SiliconLawsetPrototype>]
    private const string defaultAIRule = "Asimovpp";
    private void OnGetLaws(Entity<StationAIComponent> ent, ref GetSiliconLawsEvent args)
    {
        if (ent.Comp.SelectedLaw == null)
        {
            var selectedLaw = _prototypeManager.Index(ent.Comp.LawsId).Pick();
            if (_prototypeManager.TryIndex<SiliconLawsetPrototype>(selectedLaw, out var newLaw))
            {
                ent.Comp.SelectedLaw = newLaw;
            }
            else
            {
                ent.Comp.SelectedLaw = _prototypeManager.Index<SiliconLawsetPrototype>(defaultAIRule);
            }
        }

        foreach (var law in ent.Comp.SelectedLaw.Laws)
        {
            args.Laws.Laws.Add(_prototypeManager.Index<SiliconLawPrototype>(law));
        }

        args.Handled = true;
    }

    private void OnInit(EntityUid uid, AIEyePowerComponent component, ComponentInit args)
    {
        if (!HasComp<StationAIComponent>(uid))
            return;

        _actions.AddAction(uid, ref component.EyePowerAction, component.PrototypeAction);
    }

    private void OnShutdown(EntityUid uid, AIEyePowerComponent component, ComponentShutdown args)
    {
        if (!HasComp<StationAIComponent>(uid))
            return;

        if (component.EyePowerAction != null)
            _actions.RemoveAction(uid, component.EyePowerAction);
    }

    private void OnPowerReturnUsed(EntityUid uid, AIEyeComponent component, AIEyePowerReturnActionEvent args)
    {
        if (
            !TryComp<VisitingMindComponent>(args.Performer, out var mindId) ||
            mindId!.MindId == null ||
            !TryComp<MindComponent>(mindId.MindId.Value, out var mind)
        )
            return;

        ClearState(args.Performer);
        args.Handled = true;
    }

    private void OnPowerUsed(EntityUid uid, AIEyePowerComponent component, AIEyePowerActionEvent args)
    {
        if (_mobState.IsDead(args.Performer))
            return;

        if (!_mindSystem.TryGetMind(args.Performer, out var mindId, out var mind))
            return;

        if (!TryComp<StationAIComponent>(uid, out var ai))
            return;

        var coords = Transform(uid).Coordinates;
        var projection = EntityManager.CreateEntityUninitialized(component.Prototype, coords);
        ai.ActiveEye = projection;
        EnsureComp<AIEyeComponent>(projection).AiCore = (uid, ai);
        var eyeStation = EnsureComp<StationAIComponent>(projection);
        eyeStation.SelectedLaw = ai.SelectedLaw;
        eyeStation.SelectedLayer = ai.SelectedLayer;
        EnsureComp<SiliconLawBoundComponent>(projection);
        var core = MetaData(uid);
        // Consistent name
        _metaDataSystem.SetEntityName(projection, core.EntityName != "" ? core.EntityName : "Invalid AI");
        EntityManager.InitializeAndStartEntity(projection, coords.GetMapId(EntityManager));

        _transformSystem.AttachToGridOrMap(projection);

        _appearance.SetData(uid, AiVisuals.InEye, true);
        _mindSystem.Visit(mindId, projection, mind); // Mind swap

        args.Handled = true;
    }


    private void OnStartup(EntityUid uid, AIEyeComponent component, ComponentStartup args)
    {
        if (!HasComp<StationAIComponent>(uid) ||
            !TryComp<VisibilityComponent>(uid, out var visibility) ||
            !TryComp<EyeComponent>(uid, out var eye))
            return;

        _sharedEyeSystem.SetVisibilityMask(uid,  eye.VisibilityMask | (int) VisibilityFlags.AIEye, eye);
        _visibilitySystem.AddLayer((uid, visibility), (int) VisibilityFlags.AIEye);
        _actions.AddAction(uid, ref component.ReturnActionUid, component.ReturnAction);
        _actions.AddAction(uid, ref component.CamListUid, component.CamListAction);
        _actions.AddAction(uid, ref component.CamShootUid, component.CamShootAction);



        var pos = Transform(uid).GridUid;
        var cams = EntityQueryEnumerator<SurveillanceCameraComponent, TransformComponent>();
        while (cams.MoveNext(out var camUid, out var cam, out var transformComponent))
        {
            if(transformComponent.GridUid != pos)
                continue;
            component.FollowsCameras.Add((GetNetEntity(camUid), GetNetCoordinates(transformComponent.Coordinates)));
        }
        Dirty(uid, component);
    }

    private void OnMindRemoved(EntityUid uid, AIEyeComponent component, MindRemovedMessage args)
    {
        _uiSystem.TryCloseAll(uid, AICameraListUiKey.Key);
        QueueDel(uid);
        if(component.AiCore.HasValue)
            OnReturnToCore(component.AiCore.Value);
    }
    private void OnMindRemoved2(EntityUid uid, AIEyeComponent component, MindUnvisitedMessage args)
    {
        _uiSystem.TryCloseAll(uid, AICameraListUiKey.Key);
        QueueDel(uid);
        if(component.AiCore.HasValue)
            OnReturnToCore(component.AiCore.Value);
    }

    private void ClearState(EntityUid uid, AIEyeComponent? component = null)
    {
        if (!Resolve(uid, ref component))
        {
            return;
        }

        QueueDel(uid);
        if (!component.AiCore.HasValue)
            return;

        if (_mindSystem.TryGetMind(component.AiCore.Value, out var mindId, out var mind))
        {
            _mindSystem.UnVisit(mindId, mind);
        }

        OnReturnToCore(component.AiCore.Value);
    }

    private void OnReturnToCore(Entity<StationAIComponent> ent)
    {
        ent.Comp.ActiveEye = EntityUid.Invalid;
        _uiSystem.TryCloseAll(ent, AICameraListUiKey.Key);
        _appearance.SetData(ent, AiVisuals.InEye, false);
    }
}
