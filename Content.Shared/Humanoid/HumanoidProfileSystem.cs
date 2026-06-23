using Content.Shared.Corvax.TTS;
using Content.Shared.Examine;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.IdentityManagement;
using Content.Shared.Preferences;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects.Components.Localization;
using Robust.Shared.Prototypes;

namespace Content.Shared.Humanoid;

public sealed partial class HumanoidProfileSystem : EntitySystem
{
    public static readonly ProtoId<TTSVoicePrototype> DefaultVoice = "Aidar";

    public static readonly Dictionary<Sex, ProtoId<TTSVoicePrototype>> DefaultSexVoice = new()
    {
        { Sex.Male, "Aidar" },
        { Sex.Female, "Kseniya" },
        { Sex.Unsexed, "Baya" },
    };

    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private GrammarSystem _grammar = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidProfileComponent, ExaminedEvent>(OnExamined);
    }

    public void SetSex(Entity<HumanoidProfileComponent?> ent, Sex sex)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        var oldSex = ent.Comp.Sex;
        ent.Comp.Sex = sex;
        ent.Comp.Gender = sex switch
        {
            Sex.Male => Gender.Male,
            Sex.Female => Gender.Female,
            Sex.Unsexed => Gender.Neuter,
            _ => Gender.Epicene
        };
        Dirty(ent);

        var sexChanged = new SexChangedEvent(oldSex, sex);
        RaiseLocalEvent(ent, ref sexChanged);

        if (TryComp<GrammarComponent>(ent, out var grammar))
            _grammar.SetGender((ent, grammar), ent.Comp.Gender);
    }

    public void ApplyProfileTo(Entity<HumanoidProfileComponent?> ent, HumanoidCharacterProfile profile)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        ent.Comp.Gender = profile.Gender;
        ent.Comp.Age = profile.Age;
        ent.Comp.Species = profile.Species;
        ent.Comp.Sex = profile.Sex;
        ent.Comp.Voice = ResolveVoice(profile);
        Dirty(ent);

        if (TryComp<TTSComponent>(ent, out var tts))
        {
            tts.VoicePrototypeId = ent.Comp.Voice;
            Dirty(ent, tts);
        }

        var sexChanged = new SexChangedEvent(ent.Comp.Sex, profile.Sex);
        RaiseLocalEvent(ent, ref sexChanged);

        if (TryComp<GrammarComponent>(ent, out var grammar))
        {
            _grammar.SetGender((ent, grammar), profile.Gender);
        }
    }

    private ProtoId<TTSVoicePrototype> ResolveVoice(HumanoidCharacterProfile profile)
    {
        if (profile.Voice != default
            && _prototype.TryIndex(profile.Voice, out var voiceProto)
            && HumanoidCharacterProfile.CanHaveVoice(voiceProto, profile.Sex))
        {
            return profile.Voice;
        }

        return DefaultSexVoice.GetValueOrDefault(profile.Sex, DefaultVoice);
    }

    private void OnExamined(Entity<HumanoidProfileComponent> ent, ref ExaminedEvent args)
    {
        var identity = Identity.Entity(ent, EntityManager);
        var species = GetSpeciesRepresentation(ent.Comp.Species).ToLower();
        var age = GetAgeRepresentation(ent.Comp.Species, ent.Comp.Age);

        args.PushText(Loc.GetString("humanoid-appearance-component-examine", ("user", identity), ("age", age), ("species", species)));
    }

    /// <summary>
    /// Takes ID of the species prototype, returns UI-friendly name of the species.
    /// </summary>
    public string GetSpeciesRepresentation(ProtoId<SpeciesPrototype> species)
    {
        if (_prototype.TryIndex(species, out var speciesPrototype))
            return Loc.GetString(speciesPrototype.Name);

        Log.Error("Tried to get representation of unknown species: {speciesId}");
        return Loc.GetString("humanoid-appearance-component-unknown-species");
    }

    /// <summary>
    /// Takes ID of the species prototype and an age, returns an approximate description
    /// </summary>
    public string GetAgeRepresentation(ProtoId<SpeciesPrototype> species, int age)
    {
        if (!_prototype.TryIndex(species, out var speciesPrototype))
        {
            Log.Error("Tried to get age representation of species that couldn't be indexed: " + species);
            return Loc.GetString("identity-age-young");
        }

        if (age < speciesPrototype.YoungAge)
        {
            return Loc.GetString("identity-age-young");
        }

        if (age < speciesPrototype.OldAge)
        {
            return Loc.GetString("identity-age-middle-aged");
        }

        return Loc.GetString("identity-age-old");
    }
}
