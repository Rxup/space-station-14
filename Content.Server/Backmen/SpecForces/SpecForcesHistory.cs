namespace Content.Server.Backmen.SpecForces;

public sealed class SpecForcesHistory
{
    public TimeSpan RoundTime {get;set;}
    public string Event {get;set;} = default!;
    public string WhoCalled {get;set;} = default!;
}
