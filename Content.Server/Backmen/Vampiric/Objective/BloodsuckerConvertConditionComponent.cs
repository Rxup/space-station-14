namespace Content.Server.Backmen.Vampiric.Objective;

[RegisterComponent]
public sealed partial class BloodsuckerConvertConditionComponent : Component
{
    public int Goal = 0;

    [DataField("perPlayers")]
    public int PerPlayers = 10;

    [DataField("max")]
    public int MaxGoal = 5;

    public string ObjectiveText = "objective-bloodsucker-conv-name";
    public string DescriptionText = "objective-bloodsucker-conv-desc";
}
