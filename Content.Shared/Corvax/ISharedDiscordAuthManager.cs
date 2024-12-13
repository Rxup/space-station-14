namespace Content.Corvax.Interfaces.Shared;

public interface ISharedDiscordAuthManager
{
    public void Initialize();

    public bool IsOpt { get; }
    public bool IsEnabled { get; }
}
