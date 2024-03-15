using Robust.Shared.Audio;

namespace Content.Server.Backmen.Vampiric;

[RegisterComponent]
public sealed partial class BloodsuckerRuleComponent : Component
{
    public readonly Dictionary<string, EntityUid> Elders = new();

    public int TotalBloodsuckers = 0;

    public List<string> SpeciesWhitelist = new()
    {
        "Human",
        "Reptilian",
        "Dwarf",
        "Oni",
        "Vox",
        "HumanoidFoxes",
    };

    [DataField]
    public SoundSpecifier InitialInfectedSound = new SoundPathSpecifier("/Audio/Backmen/Ambience/Antag/vampier_start.ogg");
}
