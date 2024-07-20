using System.Linq;
using System.Text.RegularExpressions;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    private static readonly Dictionary<string, string> SlangReplace = new()
    {
        { "ббт", "неудачники" },
        { "bbt", "неудачники" },
        { "биг баллс тим", "неудачники" },
        { "биг боллс тим", "неудачники" },
        { "big balls team", "неудачники" },
        { "BIG_BALLS_TEAM", "неудачники" },
        { "▓▓▓▓▓BIG_BALLS_TEAM▓▓▓▓▓▓", "неудачники" },
        { "▓▓▓▀░░░░░▄██▄░░░░░▀▓▓▓", "и я - один из этих неудачников" },
        { "▓▓░░░░░▄▄██▀░░░░░░░░▓▓", "меня били в детстве" },
        { "▓░░░░░▄██▀░░░▄█▄░░░░░▓", "и детство моё еще не закончилось" },
        { "▌░░░░░▀██▄▄▄█████▄░░░▐", "мои папа и папа били меня" },
        { "░░▄▄▄░░░▀████▀░▀▀██▄░░", "а потом сдали в детдом" },
        { "░░▀██▄░▄▄████▄░░░▀▀▀░░", "и я не умею ничего другого" },
        { "▌░░░▀█████▀▀▀██▄░░░░░▐", "кроме как мешать жить и радоваться другим" },
        { "▓░░░░░▀█▀░░░▄██▀░░░░░▓", "пожалуйста, закончите это" },
        { "▓▓░░░░░░░░▄██▀░░░░░░▓▓", "прервите мою жизнь и жизнь таких, как я" },
    };

    private string ReplaceWords(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        return Regex.Replace(message, "\\b(\\w+)\\b", match =>
        {
            bool isUpperCase = match.Value.All(Char.IsUpper);

            if (SlangReplace.TryGetValue(match.Value.ToLower(), out var replacement))
                return isUpperCase ? replacement.ToUpper() : replacement;
            return match.Value;
        });
    }
}