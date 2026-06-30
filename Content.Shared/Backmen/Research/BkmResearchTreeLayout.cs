using System.Linq;
using System.Numerics;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Research;

/// <summary>
/// Automatically lays out technologies for the fancy R&D console.
/// </summary>
public static class BkmResearchTreeLayout
{
    private const int TierRowSpacing = 4;
    private const int DisciplineMacroColumns = 3;
    private const int MacroCellPadding = 2;

    // start-backmen: rnd-auto-layout
    public static Dictionary<string, Vector2i> ComputeLayout(
        IEnumerable<TechnologyPrototype> technologies,
        IReadOnlyList<ProtoId<TechDisciplinePrototype>>? disciplineOrder = null)
    {
        var visible = technologies.Where(t => !t.Hidden).ToList();
        var byId = visible.ToDictionary(t => t.ID);

        var disciplines = BuildDisciplineOrder(visible, disciplineOrder);
        var disciplineCells = BuildDisciplineCells(visible, disciplines);

        var macroCellWidth = (disciplineCells.Count == 0 ? 0 : disciplineCells.Values.Max(c => c.Width)) + MacroCellPadding;
        var macroCellHeight = (disciplineCells.Count == 0 ? 0 : disciplineCells.Values.Max(c => c.Height)) + MacroCellPadding;

        var result = new Dictionary<string, Vector2i>();

        for (var disciplineIndex = 0; disciplineIndex < disciplines.Count; disciplineIndex++)
        {
            var discipline = disciplines[disciplineIndex];
            if (!disciplineCells.TryGetValue(discipline, out var cell))
                continue;

            var macroX = disciplineIndex % DisciplineMacroColumns * macroCellWidth;
            var macroY = disciplineIndex / DisciplineMacroColumns * macroCellHeight;

            foreach (var (techId, localPosition) in cell.Positions)
            {
                result[techId] = new Vector2i(macroX + localPosition.X, macroY + localPosition.Y);
            }
        }

        return result;
    }

    private static List<string> BuildDisciplineOrder(
        List<TechnologyPrototype> visible,
        IReadOnlyList<ProtoId<TechDisciplinePrototype>>? disciplineOrder)
    {
        if (disciplineOrder is { Count: > 0 })
        {
            return disciplineOrder
                .Select(d => d.ToString())
                .Where(d => visible.Any(t => t.Discipline == d))
                .ToList();
        }

        return visible
            .Select(t => t.Discipline.ToString())
            .Distinct()
            .OrderBy(d => d)
            .ToList();
    }

    private static Dictionary<string, DisciplineCell> BuildDisciplineCells(
        List<TechnologyPrototype> visible,
        List<string> disciplines)
    {
        var cells = new Dictionary<string, DisciplineCell>();

        foreach (var discipline in disciplines)
        {
            var disciplineTechs = visible.Where(t => t.Discipline == discipline).ToList();
            if (disciplineTechs.Count == 0)
                continue;

            var positions = new Dictionary<string, Vector2i>();
            var maxWidth = 0;
            var maxHeight = 0;

            foreach (var tierGroup in disciplineTechs.GroupBy(t => t.Tier).OrderBy(g => g.Key))
            {
                var ordered = OrderWithinTier(tierGroup.ToList(), disciplineTechs);
                var rowY = (tierGroup.Key - 1) * TierRowSpacing;
                maxHeight = Math.Max(maxHeight, rowY + 1);

                for (var i = 0; i < ordered.Count; i++)
                {
                    positions[ordered[i].ID] = new Vector2i(i, rowY);
                    maxWidth = Math.Max(maxWidth, i + 1);
                }
            }

            cells[discipline] = new DisciplineCell(positions, maxWidth, maxHeight);
        }

        return cells;
    }

    private static List<TechnologyPrototype> OrderWithinTier(
        List<TechnologyPrototype> tierTechs,
        List<TechnologyPrototype> disciplineTechs)
    {
        var disciplineIds = disciplineTechs.Select(t => t.ID).ToHashSet();
        var byId = disciplineTechs.ToDictionary(t => t.ID);
        var depthCache = new Dictionary<string, int>();

        int GetDepth(TechnologyPrototype tech)
        {
            if (depthCache.TryGetValue(tech.ID, out var cached))
                return cached;

            var prereqs = tech.TechnologyPrerequisites
                .Select(p => p.ToString())
                .Where(disciplineIds.Contains)
                .Select(id => byId[id])
                .ToList();

            var depth = prereqs.Count == 0 ? 0 : prereqs.Max(GetDepth) + 1;
            depthCache[tech.ID] = depth;
            return depth;
        }

        foreach (var tech in tierTechs)
            GetDepth(tech);

        return tierTechs
            .OrderBy(t => depthCache[t.ID])
            .ThenBy(t => t.ID)
            .ToList();
    }

    private sealed record DisciplineCell(
        Dictionary<string, Vector2i> Positions,
        int Width,
        int Height);
    // end-backmen: rnd-auto-layout
}
