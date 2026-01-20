namespace Content.Server.Backmen.Fugitive;

[AutoGenerateComponentPause]
[RegisterComponent]
public sealed partial class FugitiveCountdownComponent : Component
{
    [AutoPausedField]
    public TimeSpan? AnnounceTime = null;

    [DataField("AnnounceCD")] public TimeSpan AnnounceCD = TimeSpan.FromMinutes(5);
}
