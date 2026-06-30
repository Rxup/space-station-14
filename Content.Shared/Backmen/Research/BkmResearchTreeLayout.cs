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
    private const int MaxTechsPerRow = 4;
    private const int TierGap = 1;
    private const int DisciplineMacroColumns = 4;
    private const int MacroCellPadding = 1;

    // start-backmen: rnd-auto-layout
    public static Dictionary<string, Vector2i> ComputeLayout(
        IEnumerable<TechnologyPrototype> technologies,
        IReadOnlyList<ProtoId<TechDisciplinePrototype>>? disciplineOrder = null)
    {
        var visible = technologies.Where(t => !t.Hidden).ToList();

        var disciplines = BuildDisciplineOrder(visible, disciplineOrder);
        var disciplineCells = BuildDisciplineCells(visible, disciplines);

        var result = new Dictionary<string, Vector2i>();

        var rowStartX = 0;
        var rowStartY = 0;
        var rowMaxHeight = 0;

        for (var disciplineIndex = 0; disciplineIndex < disciplines.Count; disciplineIndex++)
        {
            if (disciplineIndex % DisciplineMacroColumns == 0 && disciplineIndex > 0)
            {
                rowStartY += rowMaxHeight + MacroCellPadding;
                rowStartX = 0;
                rowMaxHeight = 0;
            }

            var discipline = disciplines[disciplineIndex];
            if (!disciplineCells.TryGetValue(discipline, out var cell))
                continue;

            var macroX = rowStartX;
            var macroY = rowStartY;

            rowStartX += cell.Width + MacroCellPadding;
            rowMaxHeight = Math.Max(rowMaxHeight, cell.Height);

            foreach (var (techId, localPosition) in cell.Positions)
            {
                result[techId] = new Vector2i(macroX + localPosition.X, macroY + localPosition.Y);
            }
        }

        return result;
    }

    public static Vector2i GetBounds(Dictionary<string, Vector2i> layout)
    {
        if (layout.Count == 0)
            return Vector2i.Zero;

        var maxX = layout.Values.Max(p => p.X);
        var maxY = layout.Values.Max(p => p.Y);
        return new Vector2i(maxX + 1, maxY + 1);
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
            var currentY = 0;

            foreach (var tierGroup in disciplineTechs.GroupBy(t => t.Tier).OrderBy(g => g.Key))
            {
                var ordered = OrderWithinTier(tierGroup.ToList(), disciplineTechs);
                var rowsInTier = Math.Max(1, (ordered.Count + MaxTechsPerRow - 1) / MaxTechsPerRow);

                for (var i = 0; i < ordered.Count; i++)
                {
                    var col = i % MaxTechsPerRow;
                    var subRow = i / MaxTechsPerRow;
                    positions[ordered[i].ID] = new Vector2i(col, currentY + subRow);
                    maxWidth = Math.Max(maxWidth, col + 1);
                }

                currentY += rowsInTier + TierGap;
            }

            cells[discipline] = new DisciplineCell(positions, maxWidth, currentY);
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
