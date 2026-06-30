using Content.Server.Research.Components;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using System.Linq;
using Content.Shared.Backmen.Research;

// ReSharper disable once CheckNamespace
namespace Content.Server.Research.Systems;

public sealed partial class ResearchSystem
{
    private void InitializeBkm()
    {
        SubscribeLocalEvent<ResearchServerComponent, MapInitEvent>(OnServerInit);
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
    }
}
