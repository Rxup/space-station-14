using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;

namespace Content.Shared.Backmen.Surgery;

/// <summary>
/// Temporary bridge until surgery marking steps use nubody markings groups.
/// </summary>
public static class MarkingCategoriesConversion
{
    public static string FromHumanoidVisualLayers(HumanoidVisualLayers layer) => layer.ToString();
}
