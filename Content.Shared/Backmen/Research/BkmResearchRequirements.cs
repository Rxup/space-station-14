using System.Linq;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Research.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Research;

/// <summary>
/// Helpers for displaying R&D console technology requirements in the fancy console UI.
/// </summary>
public static class BkmResearchRequirements
{
    // start-backmen: rnd-console-prereqs
    public static bool IsEligibleForTier(TechnologyPrototype technology, int tier, IPrototypeManager prototypeManager)
    {
        foreach (var prereq in technology.TechnologyPrerequisites)
        {
            if (prototypeManager.Index(prereq).Tier > tier)
                return false;
        }

        return true;
    }

    public static IEnumerable<TechnologyPrototype> GetEligibleTierTechnologies(
        TechDisciplinePrototype discipline,
        int tier,
        IPrototypeManager prototypeManager)
    {
        return prototypeManager.EnumeratePrototypes<TechnologyPrototype>()
            .Where(p => p.Discipline == discipline.ID && !p.Hidden && p.Tier == tier)
            .Where(p => IsEligibleForTier(p, tier, prototypeManager));
    }

    public static int GetTierCompletionPercentage(
        SharedResearchSystem research,
        TechnologyDatabaseComponent component,
        TechDisciplinePrototype techDiscipline,
        IPrototypeManager prototypeManager)
    {
        var allTech = prototypeManager.EnumeratePrototypes<TechnologyPrototype>()
            .Where(p => p.Discipline == techDiscipline.ID && !p.Hidden).ToList();

        if (allTech.Count == 0)
            return 0;

        var unlocked = allTech.Count(t => research.IsTechnologyUnlocked(component, t.ID));
        return (int) Math.Clamp((float) unlocked / allTech.Count * 100f, 0, 100);
    }

    public static bool IsDisciplineLockoutBlocking(
        SharedResearchSystem research,
        TechnologyPrototype technology,
        TechnologyDatabaseComponent component,
        TechDisciplinePrototype discipline)
    {
        if (component.MainDiscipline == null || component.MainDiscipline == technology.Discipline)
            return false;

        if (technology.Tier < discipline.LockoutTier)
            return false;

        return research.GetHighestDisciplineTier(component, discipline) < technology.Tier;
    }

    public static float GetTierProgress(
        SharedResearchSystem research,
        TechnologyDatabaseComponent component,
        TechDisciplinePrototype discipline,
        int tier,
        IPrototypeManager prototypeManager)
    {
        return GetTierProgressInfo(research, component, discipline, tier, 1f, prototypeManager).Progress;
    }

    public static TierProgressInfo GetTierProgressInfo(
        SharedResearchSystem research,
        TechnologyDatabaseComponent component,
        TechDisciplinePrototype discipline,
        int tier,
        float requiredRatio,
        IPrototypeManager prototypeManager)
    {
        var tierTech = GetEligibleTierTechnologies(discipline, tier, prototypeManager).ToList();

        if (tierTech.Count == 0)
        {
            return new TierProgressInfo(0, 0, 0, 0, 100, (int) (requiredRatio * 100f), 1f);
        }

        var unlocked = tierTech.Count(t => research.IsTechnologyUnlocked(component, t.ID));
        var total = tierTech.Count;
        var requiredCount = (int) Math.Ceiling(total * requiredRatio);
        var remaining = unlocked < requiredCount ? requiredCount - unlocked : 0;
        var currentPercent = (int) Math.Clamp((float) unlocked / total * 100f, 0, 100);
        var requiredPercent = (int) (requiredRatio * 100f);
        var progress = (float) unlocked / total;

        return new TierProgressInfo(unlocked, total, requiredCount, remaining, currentPercent, requiredPercent, progress);
    }

    public static bool IsTierRequirementMet(
        SharedResearchSystem research,
        TechnologyPrototype technology,
        TechnologyDatabaseComponent component)
    {
        return technology.Tier <= research.GetHighestDisciplineTier(component, technology.Discipline);
    }

    public static bool HasUnmetRequirements(
        SharedResearchSystem research,
        TechnologyPrototype technology,
        TechnologyDatabaseComponent? component,
        IPrototypeManager prototypeManager)
    {
        if (technology.TechnologyPrerequisites.Any(prereq =>
                component == null || !research.IsTechnologyUnlocked(component, prereq)))
            return true;

        if (component == null)
            return false;

        var discipline = prototypeManager.Index(technology.Discipline);

        if (IsDisciplineLockoutBlocking(research, technology, component, discipline))
            return true;

        return !IsTierRequirementMet(research, technology, component);
    }

    public readonly record struct TierProgressInfo(
        int Unlocked,
        int Total,
        int RequiredCount,
        int Remaining,
        int CurrentPercent,
        int RequiredPercent,
        float Progress);
    // end-backmen: rnd-console-prereqs
}
