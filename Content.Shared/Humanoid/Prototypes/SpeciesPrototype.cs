using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Shared.Humanoid.Prototypes;

[Prototype]
public sealed partial class SpeciesPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name { get; private set; } = default!;

    [DataField]
    public string Descriptor { get; private set; } = "humanoid";

    [DataField(required: true)]
    public bool RoundStart { get; private set; } = false;

    // Corvax-Sponsors-Start
    [DataField]
    public bool SponsorOnly { get; private set; } = false;
    // Corvax-Sponsors-End

    [DataField]
    public Color DefaultSkinTone { get; private set; } = Color.White;

    [DataField]
    public Color DefaultEyeTone { get; private set; } = Color.Black;

    [DataField]
    public int DefaultHumanSkinTone { get; private set; } = 20;

    [DataField(required: true)]
    public EntProtoId Prototype { get; private set; } = default!;

    [DataField(required: true)]
    public EntProtoId DollPrototype { get; private set; } = default!;

    [DataField(required: true)]
    public ProtoId<SkinColorationPrototype> SkinColoration { get; private set; }

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> MaleFirstNames { get; private set; } = "NamesFirstMale";

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> FemaleFirstNames { get; private set; } = "NamesFirstFemale";

    // Corvax-LastnameGender-Start
    [DataField]
    public ProtoId<LocalizedDatasetPrototype> MaleLastNames { get; private set; } = "names_last_male";

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> FemaleLastNames { get; private set; } = "names_last_female";
    // Corvax-LastnameGender-End

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> LastNames { get; private set; } = "NamesLast";

    [DataField]
    public SpeciesNaming Naming { get; private set; } = SpeciesNaming.FirstLast;

    [DataField]
    public List<Sex> Sexes { get; private set; } = new() { Sex.Male, Sex.Female };

    [DataField]
    public int MinAge = 18;

    [DataField]
    public int YoungAge = 30;

    [DataField]
    public int OldAge = 60;

    [DataField]
    public int MaxAge = 120;
}

public enum SpeciesNaming : byte
{
    First,
    FirstLast,
    FirstDashFirst,
    XnoY, // backmen: oni
    TheFirstofLast,
    FirstDashLast, // Parkstation-IPC
}
