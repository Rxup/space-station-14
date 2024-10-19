using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Reinforcement;

[Prototype("reinforcement")]
public sealed partial class ReinforcementPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("name", required: true)]
    public string Name { get; private set; } = default!;

    [DataField("job", required: true)]
    public ProtoId<JobPrototype> Job;

    [DataField("max")]
    public int MaxCount = 1;

    [DataField("min")]
    public int MinCount = 0;
}
