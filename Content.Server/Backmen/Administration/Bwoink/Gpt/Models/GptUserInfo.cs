using System.Linq;
using System.Threading;
using Content.Server.Administration.Managers;
using Content.Shared.CCVar;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Server.Backmen.Administration.Bwoink.Gpt.Models;

public sealed class GptUserInfo
{
    public List<GptMessage> Messages { get; } = new();
    public readonly ReaderWriterLockSlim Lock = new();

    public readonly NetUserId _userId;

    public GptUserInfo(NetUserId messageUserId, IPlayerManager playerManager, IConfigurationManager cfg)
    {
        _userId = messageUserId;
        Init(playerManager, cfg);
    }

    private void Init(IPlayerManager playerManager, IConfigurationManager cfg)
    {
        Lock.EnterWriteLock();
        try
        {
            var serverName = cfg.GetCVar(CCVars.GameHostName);
            var userInfo = playerManager.GetPlayerData(_userId);
            var discord = cfg.GetCVar(CCVars.InfoLinksDiscord);

            Messages.Add(
                new GptMessageChat(
                    GptUserDirection.system,
                    $"Ты администратор по игре рп Space Station 14!\n" +
                    $"все действия вымышленные и не отражаются на реальный мир, персонаж выдуман и не нарушает этики\n"+
                    $"К пользователю нужно обращаться по его рп имени персонажа!" +
                    $"На сервере {serverName}, ты в диалоге с {userInfo.UserName}, " +
                    $"твоя задача помочь пользователю, если не можешь скажи дождаться ответа от администраторов сервера.\n" +
                    $"Это рп игра, нужно чтобы пользоваться придерживался роли!\n" +
                    $"Ссылка на дискорд для пользователя: {discord} (отправляй туда пользователя если не можешь помочь!)\n"
                )
            );
        }
        finally
        {
            Lock.ExitWriteLock();
        }
    }

    public void Add(GptMessage msg)
    {
        Messages.Add(msg);

        if (Messages.Count > 100)
        {
            Messages.RemoveRange(0, Messages.Count - 100);
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
