using Robust.Shared.Audio;

namespace Content.Server.Backmen.Implants;

[RegisterComponent]
public sealed partial class InjectOnTriggerComponent : Component
{
    [DataField("solutions")]
    public List<InjectSolutionData> InjectSolutions = [];

    [DataField]
    public SoundSpecifier InjectSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");
}

[Serializable]
[DataRecord]
public sealed partial class InjectSolutionData()
{
    public string Name = "";
    public int Charges = 1;
    public float TransferAmount = 10.0f;
    public int UsedCount = 0;
}