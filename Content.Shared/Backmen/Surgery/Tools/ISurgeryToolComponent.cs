namespace Content.Shared.Backmen.Surgery.Tools;

public interface ISurgeryToolComponent
{
    public string ToolName { get; }

    // Mostly intended for discardable or non-reusable tools.
    public bool? Used { get; set; }

    /// <summary>
    ///     Multiply the step's doafter by this value.
    ///     This is per-type so you can have something that's a good scalpel but a bad retractor.
    /// </summary>
    public float Speed { get; set; }
}
