using Content.Shared.Humanoid;

namespace Content.Shared.Backmen.Surgery;

/// <summary>
/// Temporary bridge until surgery marking steps use nubody markings groups.
/// </summary>
public static class MarkingCategoriesConversion
{
    public static string FromHumanoidVisualLayers(HumanoidVisualLayers layer) => layer.ToString();
}
