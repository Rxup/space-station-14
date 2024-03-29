using Content.Server.Backmen.Psionics;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Humanoid;
using Content.Server.Speech.Components;
using Content.Server.StationEvents.Components;
using Content.Shared.Cloning;
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
    public readonly Entity<CloningPodComponent> Device;
    public readonly EntityUid Source;
    public string? Proto;
    public bool IsHandleAppearance = false;

    public CloningSpawnEvent(Entity<CloningPodComponent> device, EntityUid source)
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
        SubscribeLocalEvent<CloningEvent>(OnCloningApply);
    }

    private void OnCloningApply(ref CloningEvent ev)
    {
        EnsureComp<PotentialPsionicComponent>(ev.Target);
        if (!TryComp<HumanoidAppearanceComponent>(ev.Source, out var humanoid) || !_prototypeManager.TryIndex(humanoid.Species, out var oldSpecies))
        {
            return;
        }

        if (!TryComp<HumanoidAppearanceComponent>(ev.Target, out var newHumanoid) || !_prototypeManager.TryIndex(newHumanoid.Species, out var newSpecies)) //non human fix
        {
            RemComp<ReplacementAccentComponent>(ev.Target);
            RemComp<MonkeyAccentComponent>(ev.Target);
            RemComp<SentienceTargetComponent>(ev.Target);
            RemComp<GhostTakeoverAvailableComponent>(ev.Target);
            return;
        }

        TryComp<MetempsychosisKarmaComponent>(ev.Source, out var oldKarma);

        var applyKarma = false;

        var switchingSpecies = Prototype(ev.Source)?.ID != Prototype(ev.Target)?.ID;

        if (switchingSpecies || HasComp<MetempsychosisKarmaComponent>(ev.Source))
        {
            var pref = HumanoidCharacterProfile.RandomWithSpecies(newHumanoid.Species);
            if (oldSpecies.Sexes.Contains(humanoid.Sex))
                pref = pref.WithSex(humanoid.Sex);

            pref = pref.WithGender(humanoid.Gender);
            pref = pref.WithAge(humanoid.Age);


            _humanoidSystem.LoadProfile(ev.Target, pref);
            applyKarma = true;
        }

        if (applyKarma)
        {
            var karma = EnsureComp<MetempsychosisKarmaComponent>(ev.Target);
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

        var proto = GetSpawnEntity(args.Device, metem.KarmaBonus, speciesPrototype, oldKarma?.Score, metem);
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
            Logger.Error("Tried to get a spawn target from someone that was not a metempsychotic machine...");
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
                    Logger.Error("Could not index species for metempsychotic machine...");
                    return "MobHuman";
                }
            }
        }

        if (!_prototypeManager.TryIndex<WeightedRandomPrototype>(MetempsychoticNonHumanoidPool, out var nonHumanoidPool))
        {
            Logger.Error("Could not index the pool of non humanoids for metempsychotic machine!");
            return "MobHuman";
        }

        return nonHumanoidPool.Pick();
    }
}
