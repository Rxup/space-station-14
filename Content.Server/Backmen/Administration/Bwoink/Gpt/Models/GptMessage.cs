namespace Content.Server.Backmen.Administration.Bwoink.Gpt.Models;

public abstract class GptMessage
{
    public GptUserDirection Role { get; set; } = default!;

    public abstract object ToApi();
}
