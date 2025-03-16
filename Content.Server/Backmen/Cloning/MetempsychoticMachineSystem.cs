using Content.Server.Backmen.Psionics;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Humanoid;
using Content.Server.Speech.Components;
using Content.Server.StationEvents.Components;
using Content.Shared.Backmen.Psionics.Components;
using Content.Shared.Cloning;
using Content.Shared.Cloning.Events;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Cloning;

[ByRefEvent]
public struct CloningSpawnEvent
{
    public readonly Entity<CloningPodComponent>? Device;
    public readonly EntityUid Source;
    public string? Proto;
    public bool IsHandleAppearance = false;

    public CloningSpawnEvent(Entity<CloningPodComponent>? device, EntityUid source)
    {
        Source = source;
        Device = device;
    }
}

public sealed class MetempsychoticMachineSystem : EntitySystem
{
    [ValidatePrototypeId<WeightedRandomPrototype>]
    private const string MetempsychoticHumanoidPool = "MetempsychoticHumanoidPool";
    [ValidatePrototypeId<WeightedRandomPrototype>]
    private const string MetempsychoticNonHumanoidPool = "MetempsychoticNonhumanoidPool";

    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoidSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<Server.Backmen.Cloning.CloningSpawnEvent>(OnCloning);
        SubscribeLocalEvent<PotentialPsionicComponent, CloningEvent>(OnCloningApply);
    }

    private void OnCloningApply(Entity<PotentialPsionicComponent> ent, ref CloningEvent ev)
    {

        EnsureComp<PotentialPsionicComponent>(ev.CloneUid);
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid) || !_prototypeManager.TryIndex(humanoid.Species, out var oldSpecies))
        {
            return;
        }

        if (!TryComp<HumanoidAppearanceComponent>(ev.CloneUid, out var newHumanoid) || !_prototypeManager.TryIndex(newHumanoid.Species, out var newSpecies)) //non human fix
        {
            RemComp<ReplacementAccentComponent>(ev.CloneUid);
            RemComp<MonkeyAccentComponent>(ev.CloneUid);
            RemComp<SentienceTargetComponent>(ev.CloneUid);
            RemComp<GhostTakeoverAvailableComponent>(ev.CloneUid);
            return;
        }

        TryComp<MetempsychosisKarmaComponent>(ent, out var oldKarma);

        var applyKarma = false;

        var switchingSpecies = Prototype(ent)?.ID != Prototype(ev.CloneUid)?.ID;

        if (switchingSpecies || HasComp<MetempsychosisKarmaComponent>(ent))
        {
            var pref = HumanoidCharacterProfile.RandomWithSpecies(newHumanoid.Species);
            if (oldSpecies.Sexes.Contains(humanoid.Sex))
                pref = pref.WithSex(humanoid.Sex);

            pref = pref.WithGender(humanoid.Gender);
            pref = pref.WithAge(humanoid.Age);


            _humanoidSystem.LoadProfile(ev.CloneUid, pref);
            applyKarma = true;
        }

        if (applyKarma)
        {
            var karma = EnsureComp<MetempsychosisKarmaComponent>(ev.CloneUid);
            karma.Score++;
            if (oldKarma != null)
                karma.Score += oldKarma.Score;
        }
    }

    private void OnCloning(ref CloningSpawnEvent args)
    {
        if (!TryComp<MetempsychoticMachineComponent>(args.Device, out var metem))
        {
            return;
        }

        TryComp<MetempsychosisKarmaComponent>(args.Source, out var oldKarma);

        if (!TryComp<HumanoidAppearanceComponent>(args.Source, out var humanoid) ||
            !_prototypeManager.TryIndex(humanoid.Species, out var speciesPrototype))
        {
            return;
        }

        var proto = GetSpawnEntity(args.Device.Value, metem.KarmaBonus, speciesPrototype, oldKarma?.Score, metem);
        if (args.Proto != proto)
        {
            args.IsHandleAppearance = true;
        }

        args.Proto = proto;
    }

    public string GetSpawnEntity(EntityUid uid, float karmaBonus, SpeciesPrototype oldSpecies, int? karma = null, MetempsychoticMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
        {
            Log.Error("Tried to get a spawn target from someone that was not a metempsychotic machine...");
            return "MobHuman";
        }

        var chance = component.HumanoidBaseChance + karmaBonus;

        if (karma != null)
        {
            chance -= ((1 - component.HumanoidBaseChance) * (float) karma);
        }

        if (chance > 1)
        {
            if (_random.Prob(chance - 1))
            {
                return oldSpecies.Prototype;
            }
            else
            {
                chance = 1;
            }
        }

        chance = Math.Clamp(chance, 0, 1);

        if (_random.Prob(chance))
        {
            if (_prototypeManager.TryIndex<WeightedRandomPrototype>(MetempsychoticHumanoidPool, out var humanoidPool))
            {
                if (_prototypeManager.TryIndex<SpeciesPrototype>(humanoidPool.Pick(), out var speciesPrototype))
                {
                    return speciesPrototype.Prototype;
                }
                else
                {
                    Log.Error("Could not index species for metempsychotic machine...");
                    return "MobHuman";
                }
            }
        }

        if (!_prototypeManager.TryIndex<WeightedRandomPrototype>(MetempsychoticNonHumanoidPool, out var nonHumanoidPool))
        {
            Log.Error("Could not index the pool of non humanoids for metempsychotic machine!");
            return "MobHuman";
        }

        return nonHumanoidPool.Pick();
    }
}
