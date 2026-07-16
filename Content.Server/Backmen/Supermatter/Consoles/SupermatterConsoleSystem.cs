using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Backmen.Supermatter.Components;
using Content.Shared.Backmen.Supermatter.Consoles;
using Content.Shared.Backmen.Supermatter.Monitor;
using Content.Shared.Radiation.Components;
using Robust.Server.GameObjects;

namespace Content.Server.Backmen.Supermatter.Consoles;

public sealed partial class SupermatterConsoleSystem : SharedSupermatterConsoleSystem
{
    [Dependency] private UserInterfaceSystem _userInterfaceSystem = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SupermatterConsoleComponent, ComponentInit>(OnConsoleInit);
        SubscribeLocalEvent<SupermatterConsoleComponent, EntParentChangedMessage>(OnConsoleParentChanged);
        SubscribeLocalEvent<SupermatterConsoleComponent, SupermatterConsoleFocusChangeMessage>(OnFocusChangedMessage);
        SubscribeLocalEvent<GridSplitEvent>(OnGridSplit);
    }

    private void OnConsoleInit(EntityUid uid, SupermatterConsoleComponent component, ComponentInit args) =>
        InitializeConsole(uid, component);

    private void OnConsoleParentChanged(EntityUid uid, SupermatterConsoleComponent component, EntParentChangedMessage args) =>
        InitializeConsole(uid, component);

    private void OnFocusChangedMessage(EntityUid uid, SupermatterConsoleComponent component, SupermatterConsoleFocusChangeMessage args) =>
        component.FocusSupermatter = args.FocusSupermatter;

    private void OnGridSplit(ref GridSplitEvent args)
    {
        var allGrids = args.NewGrids.ToList();

        if (!allGrids.Contains(args.Grid))
            allGrids.Add(args.Grid);

        var query = AllEntityQuery<SupermatterConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var entConsole, out var entXform))
        {
            if (entXform.GridUid == null || !allGrids.Contains(entXform.GridUid.Value))
                continue;

            InitializeConsole(ent, entConsole);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var supermatterEntriesForEachGrid = new Dictionary<EntityUid, SupermatterConsoleEntry[]>();

        var query = AllEntityQuery<SupermatterConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var entConsole, out var entXform))
        {
            if (entXform.GridUid == null)
                continue;

            if (!supermatterEntriesForEachGrid.TryGetValue(entXform.GridUid.Value, out var supermatterEntries))
            {
                supermatterEntries = GetSupermatterStateData(entXform.GridUid.Value).ToArray();
                supermatterEntriesForEachGrid[entXform.GridUid.Value] = supermatterEntries;
            }

            var highestStatus = SupermatterStatusType.Inactive;

            foreach (var entry in supermatterEntries)
            {
                if (entry.EntityStatus > highestStatus)
                    highestStatus = entry.EntityStatus;
            }

            if (TryComp<AppearanceComponent>(ent, out var entAppearance))
                _appearance.SetData(ent, SupermatterConsoleVisuals.ComputerLayerScreen, (int) highestStatus, entAppearance);

            UpdateUIState(ent, supermatterEntries, entConsole, entXform);
        }
    }

    private void UpdateUIState(
        EntityUid uid,
        SupermatterConsoleEntry[] supermatterStateData,
        SupermatterConsoleComponent component,
        TransformComponent xform)
    {
        if (!_userInterfaceSystem.IsUiOpen(uid, SupermatterConsoleUiKey.Key))
            return;

        if (xform.GridUid == null)
            return;

        var gridUid = xform.GridUid.Value;

        // ИСПРАВЛЕНИЕ: Полная проверка перед использованием
        EntityUid? focusEntity = null;
        if (component.FocusSupermatter != null)
        {
            var entity = GetEntity(component.FocusSupermatter.Value);
            if (Exists(entity))
                focusEntity = entity;
        }

        var focusSupermatterData = GetFocusSupermatterData(focusEntity, gridUid);

        _userInterfaceSystem.SetUiState(uid,
            SupermatterConsoleUiKey.Key,
            new SupermatterConsoleBoundInterfaceState(supermatterStateData, focusSupermatterData));
    }

    private List<SupermatterConsoleEntry> GetSupermatterStateData(EntityUid gridUid)
    {
        var supermatterStateData = new List<SupermatterConsoleEntry>();

        var querySupermatters = AllEntityQuery<BkmSupermatterComponent, TransformComponent>();
        while (querySupermatters.MoveNext(out var ent, out var entSupermatter, out var entXform))
        {
            if (entXform.GridUid != gridUid || !entXform.Anchored)
                continue;

            supermatterStateData.Add(new SupermatterConsoleEntry(
                GetNetEntity(ent),
                MetaData(ent).EntityName,
                GetStatus(entSupermatter)));
        }

        return supermatterStateData;
    }

    private SupermatterFocusData? GetFocusSupermatterData(EntityUid? focusSupermatter, EntityUid gridUid)
    {
        // ИСПРАВЛЕНИЕ: Полная проверка всех условий
        if (focusSupermatter == null)
            return null;

        if (!Exists(focusSupermatter.Value))
            return null;

        if (!TryComp<TransformComponent>(focusSupermatter.Value, out var focusSupermatterXform))
            return null;

        if (!focusSupermatterXform.Anchored)
            return null;

        if (focusSupermatterXform.GridUid != gridUid)
            return null;

        if (!TryComp<BkmSupermatterComponent>(focusSupermatter.Value, out var sm))
            return null;

        if (!TryComp<RadiationSourceComponent>(focusSupermatter.Value, out var radiationComp))
            return null;

        var gasStorage = GetGasStorage(focusSupermatter.Value, sm);
        var temperature = GetTemperature(focusSupermatter.Value);
        var wasteMultiplier = GetWasteMultiplier(focusSupermatter.Value, sm);
        var tempThreshold = Atmospherics.T0C + sm.HeatPenaltyThreshold;

        return new SupermatterFocusData(
            GetNetEntity(focusSupermatter.Value),
            GetIntegrity(sm),
            sm.Power,
            radiationComp.Intensity,
            gasStorage.Values.Sum(),
            temperature,
            tempThreshold * sm.DynamicHeatResistance,
            wasteMultiplier,
            sm.GasEfficiency * 100f,
            gasStorage);
    }

    /// <summary>
    /// Returns moles of the given gas around the supermatter, without consuming the mixture.
    /// </summary>
    public float GetGas(EntityUid uid, Gas gas)
    {
        var mixture = _atmosphere.GetContainingMixture(uid, true, true);
        return mixture?.GetMoles(gas) ?? 0f;
    }

    private Dictionary<Gas, float> GetGasStorage(EntityUid uid, BkmSupermatterComponent sm)
    {
        var gasStorage = new Dictionary<Gas, float>(sm.GasStorage.Count);

        foreach (var gas in sm.GasStorage.Keys)
            gasStorage[gas] = GetGas(uid, gas) * sm.GasEfficiency;

        return gasStorage;
    }

    private float GetTemperature(EntityUid uid)
    {
        var mixture = _atmosphere.GetContainingMixture(uid, true, true);
        return mixture?.Temperature ?? 0f;
    }

    private float GetWasteMultiplier(EntityUid uid, BkmSupermatterComponent sm)
    {
        var mixture = _atmosphere.GetContainingMixture(uid, true, true);
        if (mixture == null || mixture.TotalMoles <= 0f)
            return 0.5f;

        var totalMoles = mixture.TotalMoles;
        var heatModifier = 0f;

        foreach (var (gas, data) in sm.GasDataFields)
            heatModifier += GetGas(uid, gas) / totalMoles * data.HeatPenalty;

        return Math.Max(heatModifier, 0.5f);
    }

    public float GetIntegrity(BkmSupermatterComponent sm)
    {
        var integrity = sm.Damage / sm.ExplosionPoint;
        integrity = (float) Math.Round(100 - integrity * 100, 2);
        return integrity < 0 ? 0 : integrity;
    }

    public SupermatterStatusType GetStatus(BkmSupermatterComponent sm)
    {
        if (sm.Delamming)
            return SupermatterStatusType.Delaminating;

        var integrity = GetIntegrity(sm);

        if (integrity <= 5 || sm.Damage >= sm.DamageEmergencyThreshold)
            return SupermatterStatusType.Emergency;

        if (integrity <= 25 || sm.Damage >= sm.DamageWarningThreshold)
            return SupermatterStatusType.Danger;

        if (integrity <= 50 || sm.Damage > sm.DamageArchived)
            return SupermatterStatusType.Warning;

        if (sm.Power >= sm.PowerPenaltyThreshold)
            return SupermatterStatusType.Caution;

        if (sm.Power > 0)
            return SupermatterStatusType.Normal;

        return SupermatterStatusType.Inactive;
    }

    private void InitializeConsole(EntityUid uid, SupermatterConsoleComponent component)
    {
        // ИСПРАВЛЕНИЕ: Используем TryComp вместо Transform()
        if (!TryComp<TransformComponent>(uid, out var xform) || xform.GridUid == null)
            return;

        Dirty(uid, component);
    }
}
