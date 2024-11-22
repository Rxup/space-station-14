namespace Content.Server.Backmen.Traits.Assorted;

/// <summary>
///     Used for traits that add a starting moodlet.
/// </summary>
[RegisterComponent]
public sealed partial class MoodModifyTraitComponent : Component
{
    [DataField]
    public string? MoodId = null;
}
