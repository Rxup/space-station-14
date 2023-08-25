namespace Content.Server.Backmen.SpecForces;

public sealed class SpecForcesHistory
{
    public TimeSpan RoundTime {get;set;}
    public SpecForcesType Event {get;set;}
    public string WhoCalled {get;set;} = default!;
}
