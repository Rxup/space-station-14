namespace Content.Server.Backmen.Mind;

public sealed class BMemory
{
    [ViewVariables]
    public string Name { get; set; }
    [ViewVariables]
    public string Value { get; set; }
    public BMemory(string name, string value)
    {
        Name = name;
        Value = value;
    }
}
