using System.Linq;
using System.Threading;

namespace Content.Server.Backmen.Administration.Bwoink.Gpt.Models;

public record GptUserInfo
{
    public List<GptMessage> Messages { get; } = new();
    public readonly ReaderWriterLockSlim Lock = new();

    public void Add(GptMessage msg)
    {
        Messages.Add(msg);

        if (Messages.Count > 50)
        {
            Messages.RemoveRange(0, Messages.Count - 50);
        }
    }

    public bool IsCanAnswer()
    {
        return Messages.Last().Role == GptUserDirection.user;
    }

    public object[] GetMessagesForApi()
    {
        return Messages.Select(x => x.ToApi()).ToArray();
    }
}
