namespace Content.Server.Backmen.Vampiric.Objective;

[RegisterComponent]
public sealed partial class BloodsuckerDrinkConditionComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public float Goal = 0;
    [DataField("min")]
    public int MinGoal = 400;
    [DataField("max")]
    public int MaxGoal = 800;

    public string ObjectiveText = "objective-bloodsucker-drink-name";
    public string DescriptionText = "objective-bloodsucker-drink-desc";
}
