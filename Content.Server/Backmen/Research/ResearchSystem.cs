using System.Diagnostics.CodeAnalysis;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Research.Components;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using System.Linq;
using Content.Shared.Atmos;
using Content.Shared.Backmen.Research;
using Content.Shared.Examine;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Server.Research.Systems;

public sealed partial class ResearchSystem
{
    [Dependency] private SharedPowerReceiverSystem _powerReceiver = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;

    private void InitializeBkm()
    {
        SubscribeLocalEvent<ResearchServerComponent, MapInitEvent>(OnServerInit);
        SubscribeLocalEvent<ResearchServerScalingPowerComponent, ExaminedEvent>(OnServerPowerExamined);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        var techsChanged = args.WasModified<TechnologyPrototype>() || args.WasModified<TechDisciplinePrototype>();
        var recipesChanged = args.WasModified<LatheRecipePrototype>();

        if (!techsChanged && !recipesChanged)
            return;

        var servers = EntityQueryEnumerator<ResearchServerComponent, TechnologyDatabaseComponent>();
        while (servers.MoveNext(out var uid, out _, out var db))
        {
            SyncUnlockedRecipesFromTechnologies((uid, db));

            if (!techsChanged)
                continue;

            UpdateTechnologyCards(uid, db);
            UpdateResearchServerPower(uid);
        }

        if (!techsChanged)
            return;

        var consoles = EntityQueryEnumerator<ResearchConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var console))
        {
            if (!_uiSystem.IsUiOpen(uid, ResearchConsoleUiKey.Key))
                continue;

            SyncClientWithServer(uid);
            UpdateConsoleInterface(uid, console);
        }
    }

    /// <summary>
    /// Rebuilds cached lathe unlocks from currently loaded technology prototypes.
    /// Needed after runtime YAML uploads, which do not recreate research databases.
    /// </summary>
    private void SyncUnlockedRecipesFromTechnologies(Entity<TechnologyDatabaseComponent> entity)
    {
        var recipes = new HashSet<ProtoId<LatheRecipePrototype>>();

        foreach (var techId in entity.Comp.UnlockedTechnologies)
        {
            if (!PrototypeManager.TryIndex(techId, out TechnologyPrototype? tech))
                continue;

            foreach (var recipe in tech.RecipeUnlocks)
                recipes.Add(recipe);
        }

        var oldRecipes = entity.Comp.UnlockedRecipes.ToHashSet();
        if (oldRecipes.SetEquals(recipes))
            return;

        var newlyUnlocked = recipes
            .Where(recipe => !oldRecipes.Contains(recipe))
            .Select(recipe => recipe.Id)
            .ToList();

        entity.Comp.UnlockedRecipes = recipes.ToList();
        Dirty(entity, entity.Comp);

        var ev = new TechnologyDatabaseModifiedEvent(newlyUnlocked);
        RaiseLocalEvent(entity, ref ev);
    }

    private void UpdateFancyConsoleInterface(EntityUid uid,
        ResearchConsoleComponent? component = null,
        ResearchClientComponent? clientComponent = null)
    {
        if (!Resolve(uid, ref component, ref clientComponent, false))
            return;

        var allTechs = PrototypeManager.EnumeratePrototypes<TechnologyPrototype>().ToList();
        Dictionary<string, ResearchAvailability> techList;
        var points = 0;

        if (TryGetClientServer(uid, out var serverUid, out var server, clientComponent) &&
            TryComp<TechnologyDatabaseComponent>(serverUid, out var db))
        {
            var disciplineTiers = GetDisciplineTiers(db);
            techList = allTechs.ToDictionary(
                proto => proto.ID,
                proto =>
                {
                    if (db.UnlockedTechnologies.Contains(proto))
                        return ResearchAvailability.Researched;

                    if (proto.Hidden)
                        return ResearchAvailability.Unavailable;

                    var canAfford = server.Points >= proto.Cost;
                    var available = IsTechnologyAvailable(db, proto, disciplineTiers);

                    if (available && canAfford)
                        return ResearchAvailability.Available;

                    if (available)
                        return ResearchAvailability.PrereqsMet;

                    return ResearchAvailability.Unavailable;
                });

            points = clientComponent.ConnectedToServer ? server.Points : 0;
        }
        else
        {
            techList = allTechs.ToDictionary(proto => proto.ID, _ => ResearchAvailability.Unavailable);
        }

        _uiSystem.SetUiState(uid,
            ResearchConsoleUiKey.Key,
            new ResearchConsoleBoundInterfaceState(points, techList));
    }

    private void OnServerInit(Entity<ResearchServerComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<TechnologyDatabaseComponent>(ent, out var techBase))
            return;

        foreach (var tech in techBase.RoundstartTechnologies)
        {
            AddTechnology(ent, tech, techBase);
        }

        UpdateResearchServerPower(ent);
    }

    private void OnServerPowerExamined(Entity<ResearchServerScalingPowerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!TryGetResearchServerAtmosphere(ent, out var mix))
            args.PushMarkup(Loc.GetString("research-server-no-atmosphere-examine"));
        else if (mix.Temperature > ent.Comp.MaxTemperature)
        {
            args.PushMarkup(Loc.GetString("research-server-too-hot-examine",
                ("temp", mix.Temperature),
                ("max", ent.Comp.MaxTemperature)));
        }

        if (!TryComp<TechnologyDatabaseComponent>(ent, out var db))
            return;

        var techs = GetResearchedTechnologyCount(db, ent.Comp);
        var load = GetResearchServerPowerLoad(ent.Comp, techs);
        args.PushMarkup(Loc.GetString("research-server-power-examine", ("load", (int) load), ("techs", techs)));
    }

    private void UpdateResearchServerPower(EntityUid uid)
    {
        if (!TryComp<ResearchServerScalingPowerComponent>(uid, out var scaling) ||
            !TryComp<TechnologyDatabaseComponent>(uid, out var db))
            return;

        var techs = GetResearchedTechnologyCount(db, scaling);
        _powerReceiver.SetLoad(uid, GetResearchServerPowerLoad(scaling, techs));
    }

    private static int GetResearchedTechnologyCount(
        TechnologyDatabaseComponent db,
        ResearchServerScalingPowerComponent scaling)
    {
        if (!scaling.IgnoreRoundstart)
            return db.UnlockedTechnologies.Distinct().Count();

        var roundstart = db.RoundstartTechnologies.ToHashSet();
        return db.UnlockedTechnologies.Distinct().Count(tech => !roundstart.Contains(tech));
    }

    private static float GetResearchServerPowerLoad(ResearchServerScalingPowerComponent scaling, int techs)
    {
        return scaling.BaseLoad + scaling.LoadPerTechnology * Math.Max(0, techs);
    }

    private bool HasResearchServerAtmosphere(EntityUid uid)
    {
        if (!TryGetResearchServerAtmosphere(uid, out var mix))
            return false;

        if (!TryComp<ResearchServerScalingPowerComponent>(uid, out var scaling))
            return true;

        return mix.Temperature <= scaling.MaxTemperature;
    }

    private bool TryGetResearchServerAtmosphere(EntityUid uid, [NotNullWhen(true)] out GasMixture? mix)
    {
        mix = _atmosphere.GetContainingMixture(uid, true);
        if (mix == null || mix.TotalMoles <= 0f || mix.Pressure <= 0f)
        {
            mix = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Whether the R&amp;D server is actually running (powered, has air, not overheated).
    /// </summary>
    public bool IsResearchServerOperating(EntityUid uid)
    {
        return CanRun(uid);
    }
}
