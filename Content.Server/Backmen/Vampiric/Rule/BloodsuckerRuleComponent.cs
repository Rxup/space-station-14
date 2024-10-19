namespace Content.Server.Backmen.Vampiric.Rule;

[RegisterComponent]
public sealed partial class BloodsuckerRuleComponent : Component
{
    public readonly Dictionary<string, EntityUid> Elders = new();

    public int TotalBloodsuckers = 0;
}
