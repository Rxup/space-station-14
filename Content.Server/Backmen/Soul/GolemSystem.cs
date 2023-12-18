using System.Numerics;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Interaction;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Throwing;
using Content.Shared.Toggleable;
using Content.Shared.Backmen.Soul;
using Content.Shared.Dataset;
using Content.Shared.Mobs;
using Content.Shared.Administration.Logs;
using Content.Shared.Humanoid;
using Content.Server.Speech;
using Content.Server.Mind;
using Content.Shared.Backmen.Psionics.Events;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Robust.Server.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Soul;

public sealed class GolemSystem : SharedGolemSystem
{
    [Dependency] private readonly ItemSlotsSystem _slotsSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly AudioSystem _audioSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;

    private const string CrystalSlot = "crystal_slot";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GolemComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<GolemComponent, EntRemovedFromContainerMessage>(OnEntRemoved);

        SubscribeLocalEvent<SoulCrystalComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<GolemComponent, DispelledEvent>(OnDispelled);
        SubscribeLocalEvent<GolemComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<GolemComponent, GolemInstallRequestMessage>(OnInstallRequest);
        SubscribeLocalEvent<GolemComponent, GolemNameChangedMessage>(OnNameChanged);
        SubscribeLocalEvent<GolemComponent, GolemMasterNameChangedMessage>(OnMasterNameChanged);
        SubscribeLocalEvent<GolemComponent, AccentGetEvent>(OnGetAccent); // TODO: Deduplicate
        SubscribeLocalEvent<GolemComponent, GetSiliconLawsEvent>(OnGetLaws); // TODO: Deduplicate
    }

    private void OnGetLaws(EntityUid uid, GolemComponent component, ref GetSiliconLawsEvent args)
    {
        if (args.Handled || component.Master == null)
            return;

        // Add the first emag law
        args.Laws.Laws.Add(new SiliconLaw
        {
            LawString = Loc.GetString("law-golem-1", ("name", component.Master)),
            Order = 1
        });

        args.Handled = true;
    }

    private void OnEntInserted(EntityUid uid, SharedGolemComponent component, EntInsertedIntoContainerMessage args)
    {
        SharedOnEntInserted(args);
    }

    private void OnEntRemoved(EntityUid uid, SharedGolemComponent component, EntRemovedFromContainerMessage args)
    {
        SharedOnEntRemoved(args);
    }

    [ValidatePrototypeId<EntityPrototype>]
    private const string AdminObserver = "AdminObserver";
    private void OnAfterInteract(EntityUid uid, SoulCrystalComponent component, AfterInteractEvent args)
    {
        if (!args.CanReach)
            return;

        if (!TryComp<GolemComponent>(args.Target, out var golem))
            return;

        if (HasComp<ActorComponent>(args.Target))
            return;

        if (!TryComp<ActorComponent>(args.User, out var userActor))
            return;

        if (!(HasComp<HumanoidAppearanceComponent>(args.User) || Prototype(args.User)?.ID == AdminObserver))
            return;

        if (!_uiSystem.TryOpen(args.Target.Value, GolemUiKey.Key, userActor!.PlayerSession))
            return;

        golem.PotentialCrystal = uid;

        if (!_uiSystem.TryGetUi(args.Target.Value, GolemUiKey.Key, out var ui))
        {
            return;
        }

        var golemName = "golem";
        if (_prototypes.TryIndex<DatasetPrototype>("names_golem", out var names))
            golemName = _robustRandom.Pick(names.Values);

        golem.GolemName = golemName;
        golem.Master = MetaData(args.User).EntityName;

        var state = new GolemBoundUserInterfaceState(golem.GolemName, golem.Master);
        _userInterfaceSystem.SetUiState(ui, state);
    }

    private void OnDispelled(EntityUid uid, GolemComponent component, DispelledEvent args)
    {
        _slotsSystem.SetLock(uid, CrystalSlot, false);
        _slotsSystem.TryEject(uid, CrystalSlot, null, out var item);
        _slotsSystem.SetLock(uid, CrystalSlot, true);

        if (item == null)
            return;

        args.Handled = true;

        var direction = new Vector2(_robustRandom.Next(-30, 30), _robustRandom.Next(-30, 30));
        _throwing.TryThrow(item.Value, direction, _robustRandom.Next(1, 10));

        if (TryComp<AppearanceComponent>(uid, out var appearance))
            _appearance.SetData(uid, ToggleVisuals.Toggled, false, appearance);

        if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
            return;

        _metaDataSystem.SetEntityName(uid, Loc.GetString("golem-base-name"));
        _metaDataSystem.SetEntityDescription(uid, Loc.GetString("golem-base-desc"));
        _mindSystem.TransferTo(mindId, item);
        DirtyEntity(uid);
    }

    private void OnMobStateChanged(EntityUid uid, GolemComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        QueueDel(uid);
        var ev = new DispelledEvent();
        RaiseLocalEvent(uid, ev, false);

        Spawn("Ash", Transform(uid).Coordinates);
        _audioSystem.PlayPvs(component.DeathSound, uid);
    }

    private void OnInstallRequest(EntityUid uid, GolemComponent component, GolemInstallRequestMessage args)
    {
        if (component.PotentialCrystal == null)
            return;

        if (args.Session.AttachedEntity == null)
            return;

        if (!TryComp<ItemSlotsComponent>(uid, out var slots))
            return;

        if (!TryComp<ActorComponent>(component.PotentialCrystal, out var actor))
            return;

        if (!_mindSystem.TryGetMind(component.PotentialCrystal.Value, out var mindId, out var mind))
            return;

        if (!_slotsSystem.TryGetSlot(uid, CrystalSlot, out var crystalSlot, slots)) // does it not have a crystal slot?
            return;

        if (_slotsSystem.GetItemOrNull(uid, CrystalSlot, slots) != null) // is the crystal slot occupied?
            return;

        // Toggle the lock and insert the crystal.
        _slotsSystem.SetLock(uid, CrystalSlot, false, slots);
        var success = _slotsSystem.TryInsert(uid, CrystalSlot, component.PotentialCrystal.Value, args.Session.AttachedEntity.Value, slots);
        _slotsSystem.SetLock(uid, CrystalSlot, true, slots);

        if (!success)
            return;

        _uiSystem.TryCloseAll(uid);

        RemComp<GhostTakeoverAvailableComponent>(component.PotentialCrystal.Value);

        if (!string.IsNullOrEmpty(component.GolemName))
        {
            _metaDataSystem.SetEntityName(uid, component.GolemName);
        }
        else
        {
            if (_prototypes.TryIndex<DatasetPrototype>("names_golem", out var names))
            {
                _metaDataSystem.SetEntityName(uid, _robustRandom.Pick(names.Values));
            }
        }
        _metaDataSystem.SetEntityDescription(uid, Loc.GetString("golem-installed-desc"));


        if (string.IsNullOrEmpty(component.Master))
        {
            component.Master = MetaData(args.Session.AttachedEntity.Value).EntityName;
        }

        _mindSystem.TransferTo(mindId, uid);

        if (TryComp<AppearanceComponent>(uid, out var appearance))
            _appearance.SetData(uid, ToggleVisuals.Toggled, true, appearance);

        _adminLogger.Add(Shared.Database.LogType.Action, Shared.Database.LogImpact.High, $"{ToPrettyString(args.Session.AttachedEntity.Value):player} created a golem named {ToPrettyString(uid):target} obeying a master named {(component.Master)}");

        component.PotentialCrystal = null;
        //component.Master = null;
        component.GolemName = null;
        DirtyEntity(uid);
        Dirty(uid, component);
    }

    private void OnNameChanged(EntityUid uid, GolemComponent golemComponent, GolemNameChangedMessage args)
    {
        golemComponent.GolemName = args.Name;
    }

    private void OnMasterNameChanged(EntityUid uid, GolemComponent golemComponent, GolemMasterNameChangedMessage args)
    {
        golemComponent.Master = args.MasterName;
    }

    // todo deduplicate
    private void OnGetAccent(EntityUid uid, GolemComponent component, AccentGetEvent args)
    {
        args.Message = args.Message.ToUpper();
    }
}
