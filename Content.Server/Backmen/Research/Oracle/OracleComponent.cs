using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Research.Oracle;

[RegisterComponent]
public sealed partial class OracleComponent : Component
{
    public const string SolutionName = "fountain";

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("accumulator")]
    public float Accumulator = 0f;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("resetTime")]
    public TimeSpan ResetTime = TimeSpan.FromMinutes(10);

    [DataField("barkAccumulator"), ViewVariables(VVAccess.ReadWrite)]
    public float BarkAccumulator = 0f;

    [DataField("barkTime"), ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan BarkTime = TimeSpan.FromMinutes(1);

    [ViewVariables(VVAccess.ReadWrite)]
    public EntityPrototype DesiredPrototype = default!;

    [ViewVariables(VVAccess.ReadWrite)]
    public EntityPrototype? LastDesiredPrototype = default!;
}
