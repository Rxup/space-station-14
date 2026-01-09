using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Xenobiology.XenobiologyBountyConsole;

[DataDefinition, NetSerializable, Serializable]
public sealed partial class XenobiologyBountyData
{
    /// <summary>
    /// A unique id used to identify the bounty
    /// </summary>
    [DataField]
    public string Id { get; private set; } = string.Empty;

    /// <summary>
    /// The prototype containing information about the bounty.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<XenobiologyBountyPrototype> Bounty { get; private set; } = string.Empty;

    public XenobiologyBountyData(XenobiologyBountyPrototype bounty, int uniqueIdentifier)
    {
        Bounty = bounty.ID;
        Id = $"{bounty.IdPrefix}{uniqueIdentifier:D3}";
    }
}
