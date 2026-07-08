using Content.Server.Backmen.Disease.Components;
using Content.Server.Backmen.Disease.Server;
using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Server.Research.Systems;
using Content.Shared.Backmen.Disease;
using Content.Shared.Backmen.Disease.Events;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Research.Components;
using Content.Shared.UserInterface;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Disease;

public sealed partial class VaccineSystem : EntitySystem
{
    [Dependency] private DiseaseDiagnosisSystem _diseaseDiagnosisSystem = default!;
    [Dependency] private SharedMaterialStorageSystem _storageSystem = default!;
    [Dependency] private UserInterfaceSystem _uiSys = default!;
    [Dependency] private ResearchSystem _research = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private DoAfterSystem _doAfterSystem = default!;
    [Dependency] private AudioSystem _audioSystem = default!;
    [Dependency] private MetaDataSystem _metaData = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<DiseaseVaccineCreatorComponent>(VaccineMachineUiKey.Key, subscriber =>
        {
            subscriber.Event<CreateVaccineMessage>(OnCreateVaccineMessageReceived);
            subscriber.Event<ResearchClientServerSelectedMessage>(OnServerSelected);
            subscriber.Event<ResearchClientServerDeselectedMessage>(OnServerDeselected);
            subscriber.Event<VaccinatorSyncRequestMessage>(OnSyncRequest);
            subscriber.Event<VaccinatorServerSelectionMessage>(OpenServerList);
        });
        SubscribeLocalEvent<DiseaseVaccineCreatorComponent, AfterActivatableUIOpenEvent>(AfterUIOpen);
        SubscribeLocalEvent<DiseaseVaccineCreatorComponent, DiseaseMachineFinishedEvent>(OnVaccinatorFinished);
        SubscribeLocalEvent<DiseaseVaccineCreatorComponent, MaterialAmountChangedEvent>(OnVaccinatorAmountChanged);
        SubscribeLocalEvent<DiseaseVaccineCreatorComponent, ResearchRegistrationChangedEvent>(OnResearchRegistrationChanged);
        SubscribeLocalEvent<DiseaseVaccineCreatorComponent, GetMaterialWhitelistEvent>(OnGetMaterialWhitelist);

        // vaccines, the item
        SubscribeLocalEvent<DiseaseVaccineComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<DiseaseVaccineComponent, ExaminedEvent>(OnExamined);

        SubscribeLocalEvent<DiseaseVaccineComponent, VaccineDoAfterEvent>(OnDoAfter);
    }


    private void OnResearchRegistrationChanged(EntityUid uid, DiseaseVaccineCreatorComponent component, ref ResearchRegistrationChangedEvent args)
    {
        component.DiseaseServer = TryComp<DiseaseServerComponent>(args.Server, out var diseaseServer) ? diseaseServer : null;
    }

    private void OnGetMaterialWhitelist(EntityUid uid, DiseaseVaccineCreatorComponent component, ref GetMaterialWhitelistEvent args)
    {
        if (args.Storage != uid)
            return;

        if (!args.Whitelist.Contains("Biomass"))
            args.Whitelist.Add("Biomass");
    }

    /// <summary>
    /// Creates a vaccine, if possible, when sent a UI message to do so.
    /// </summary>
    private void OnCreateVaccineMessageReceived(EntityUid uid, DiseaseVaccineCreatorComponent component, CreateVaccineMessage args)
    {
        if (HasComp<DiseaseMachineRunningComponent>(uid) || !this.IsPowered(uid, EntityManager))
            return;

        if (_storageSystem.GetMaterialAmount(uid, "Biomass") < component.BiomassCost * args.Amount)
            return;

        if (!_prototypeManager.TryIndex<DiseasePrototype>(args.Disease, out var disease))
            return;

        if (!disease.Infectious)
            return;

        component.Queued = args.Amount;
        QueueNext(uid, component, disease);
        UpdateUserInterfaceState(uid, component, true);
    }
    private void QueueNext(EntityUid uid, DiseaseVaccineCreatorComponent component, DiseasePrototype disease, DiseaseMachineComponent? machine = null)
    {
        if (!Resolve(uid, ref machine))
            return;

        machine.Disease = disease;
        EnsureComp<DiseaseMachineRunningComponent>(uid);
        _diseaseDiagnosisSystem.UpdateAppearance(uid, true, true);
        _audioSystem.PlayPvs(component.RunningSoundPath, uid);
    }

    /// <summary>
    /// Prints a vaccine that will vaccinate
    /// against the disease on the inserted swab.
    /// </summary>
    private void OnVaccinatorFinished(EntityUid uid, DiseaseVaccineCreatorComponent component, DiseaseMachineFinishedEvent args)
    {
        _diseaseDiagnosisSystem.UpdateAppearance(uid, this.IsPowered(uid, EntityManager), false);

        if (!_storageSystem.TryChangeMaterialAmount(uid, "Biomass", (0 - component.BiomassCost)))
            return;

        // spawn a vaccine
        var vaxx = Spawn(args.Machine.MachineOutput, Transform(uid).Coordinates);

        if (args.Machine.Disease == null)
            return;

        _metaData.SetEntityName(vaxx, Loc.GetString("vaccine-name", ("disease", Loc.GetString(args.Machine.Disease.Name))));
        _metaData.SetEntityDescription(vaxx, Loc.GetString("vaccine-desc", ("disease", Loc.GetString(args.Machine.Disease.Name))));

        if (!TryComp<DiseaseVaccineComponent>(vaxx, out var vaxxComp))
            return;

        vaxxComp.Disease = args.Machine.Disease;

        component.Queued--;
        if (component.Queued > 0)
        {
            args.Dequeue = false;
            QueueNext(uid, component, args.Machine.Disease, args.Machine);
            UpdateUserInterfaceState(uid, component);
        }
        else
        {
            UpdateUserInterfaceState(uid, component, false);
        }
    }

    private void OnVaccinatorAmountChanged(EntityUid uid, DiseaseVaccineCreatorComponent component, ref MaterialAmountChangedEvent args)
    {
        UpdateUserInterfaceState(uid, component);
    }

    private void OnServerSelected(EntityUid uid, DiseaseVaccineCreatorComponent component, ResearchClientServerSelectedMessage args)
    {
        if (!_research.TryGetServerById(uid, args.ServerId, out var serverUid, out _))
            return;

        if (!TryComp<DiseaseServerComponent>(serverUid, out var diseaseServer))
            return;

        component.DiseaseServer = diseaseServer;
        UpdateUserInterfaceState(uid, component);
    }

    private void OnServerDeselected(EntityUid uid, DiseaseVaccineCreatorComponent component, ResearchClientServerDeselectedMessage args)
    {
        component.DiseaseServer = null;
        UpdateUserInterfaceState(uid, component);
    }

    private void OnSyncRequest(EntityUid uid, DiseaseVaccineCreatorComponent component, VaccinatorSyncRequestMessage args)
    {
        UpdateUserInterfaceState(uid, component);
    }

    private void OpenServerList(EntityUid uid, DiseaseVaccineCreatorComponent component, VaccinatorServerSelectionMessage args)
    {
        _uiSys.TryOpenUi(uid, ResearchClientUiKey.Key, args.Actor);
    }

    private void AfterUIOpen(EntityUid uid, DiseaseVaccineCreatorComponent component, AfterActivatableUIOpenEvent args)
    {
        UpdateUserInterfaceState(uid, component);
    }

    public void UpdateUserInterfaceState(EntityUid uid, DiseaseVaccineCreatorComponent? component = null, bool? overrideLocked = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var biomass = _storageSystem.GetMaterialAmount(uid, "Biomass");

        var diseases = new List<(string id, string name)>();
        var hasServer = false;

        if (component.DiseaseServer != null)
        {
            foreach (var disease in component.DiseaseServer.Diseases)
            {
                if (!disease.Infectious)
                    continue;

                diseases.Add((disease.ID, disease.Name));
            }

            hasServer = true;
        }

        var state = new VaccineMachineUpdateState(biomass, component.BiomassCost, diseases, overrideLocked ?? HasComp<DiseaseMachineRunningComponent>(uid), hasServer);
        _uiSys.SetUiState(uid, VaccineMachineUiKey.Key, state);
    }

    /// <summary>
    /// Called when a vaccine is used on someone
    /// to handle the vaccination doafter
    /// </summary>
    private void OnAfterInteract(EntityUid uid, DiseaseVaccineComponent vaxx, AfterInteractEvent args)
    {
        if (args.Target == null || !args.CanReach)
            return;

        if (vaxx.Used)
        {
            _popupSystem.PopupEntity(Loc.GetString("vaxx-already-used"), args.User, args.User);
            return;
        }

        var ev = new VaccineDoAfterEvent();
        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, vaxx.InjectDelay, ev, uid, target: args.Target, used: uid)
        {
            BreakOnMove = true,
            NeedHand = true
        };

        _doAfterSystem.TryStartDoAfter(doAfterArgs);
    }

    /// <summary>
    /// Called when a vaccine is examined.
    /// Currently doesn't do much because
    /// vaccines don't have unique art with a seperate
    /// state visualizer.
    /// </summary>
    private void OnExamined(EntityUid uid, DiseaseVaccineComponent vaxx, ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
        {
            if (vaxx.Used)
                args.PushMarkup(Loc.GetString("vaxx-used"));
            else
                args.PushMarkup(Loc.GetString("vaxx-unused"));
        }
    }
    /// <summary>
    /// Adds a disease to the carrier's
    /// past diseases to give them immunity
    /// IF they don't already have the disease.
    /// </summary>
    public bool Vaccinate(DiseaseCarrierComponent carrier, DiseasePrototype disease)
    {
        foreach (var currentDisease in carrier.Diseases)
        {
            if (currentDisease.ID == disease.ID) //ID because of the way protoypes work
                return false;
        }

        if (!disease.Infectious)
        {
            return false;
        }
        carrier.PastDiseases.Add(disease.ID);
        return true;
    }

    private void OnDoAfter(EntityUid uid, DiseaseVaccineComponent component, VaccineDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || !TryComp<DiseaseCarrierComponent>(args.Args.Target, out var carrier) || component.Disease == null)
            return;

        Vaccinate(carrier, component.Disease);
        QueueDel(uid);
        args.Handled = true;
    }
}

/// <summary>
/// Fires when a disease machine is done
/// with its production delay and ready to
/// create a report or vaccine
/// </summary>
public sealed class DiseaseMachineFinishedEvent : EntityEventArgs
{
    public DiseaseMachineComponent Machine {get;}
    public bool Dequeue = true;
    public DiseaseMachineFinishedEvent(DiseaseMachineComponent machine, bool dequeue)
    {
        Machine = machine;
        Dequeue = dequeue;
    }
}
