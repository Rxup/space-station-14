using JetBrains.Annotations;
using Robust.Shared.Random;

namespace Content.Server.Maps.NameGenerators;

[UsedImplicitly]
public sealed partial class NanotrasenCentComNameGenerator : StationNameGenerator
{

    [DataField("prefixCreator")] public string PrefixCreator = default!;

    private string Prefix => "NT";
    private string[] SuffixSectors => new[] { "Lambda", "Sigma", "Kaseopy", "Gase", "Pulla", "Neo", "Falla", "Backed", "Alean", "Litto", "Kira", "Vissa", "Deoth" };
    private string[] SuffixStates => new[]
    {
        "S", // Активная деятельность Синдиката
        "N", // Нормально
        "mN", // Много халатности
        "G", // Замечательно
        "R", // Высокий риск востаний
        "lE", // Малая эфективность
        "nC", // Недостатки персонала
        "D", // Частые зачистки
        "A", // Высокая численость чужих
        "lK", // Малая квалицикация
        "C", // Частые перебои связи
        "H", // Многочисленость юмористов
        "rB", // Частые игнорирование КЗ и СРП
        "M", // Активная деятельнсть Космической Федерации Магов
        "F", // Убытки
        "T", // Затяжные смены
        "Z" // Наличие карантийных зон
    };

    public override string FormatName(string input)
    {
        var random = IoCManager.Resolve<IRobustRandom>();

        return string.Format(input, $"{Prefix}{PrefixCreator}", $"{random.Pick(SuffixSectors)}-{random.Pick(SuffixStates)}");
    }
}
