using System.Linq;
using Content.Shared.Administration;
using Content.Shared.Administration.Managers;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Database;
using Content.Shared.Electrocution;
using Content.Shared.StatusEffect;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared.Backmen.Psionics;

public abstract class SharedPsionicsSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminManager _adminManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedElectrocutionSystem _electrocutionSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        PsionicQuery = GetEntityQuery<PsionicComponent>();
        PotentialPsionicQuery = GetEntityQuery<PotentialPsionicComponent>();

        SubscribeLocalEvent<PotentialPsionicComponent,GetVerbsEvent<Verb>>(GetVerbs);

        SubscribeLocalEvent<AntiPsionicWeaponComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<AntiPsionicWeaponComponent, StaminaMeleeHitEvent>(OnStamHit);
    }

    protected EntityQuery<PotentialPsionicComponent> PotentialPsionicQuery { get; set; }
    protected EntityQuery<PsionicComponent> PsionicQuery { get; set; }

    [ValidatePrototypeId<StatusEffectPrototype>]
    private const string PsionicsDisabled = "PsionicsDisabled";


    private static readonly SoundPathSpecifier Lightburn = new("/Audio/Effects/lightburn.ogg");

    protected virtual void RemovePsionics(Entity<PotentialPsionicComponent?> ent)
    {

    }

    protected virtual void AddPsionics(Entity<PotentialPsionicComponent?> ent)
    {

    }

    protected virtual bool UndoMindSwap(EntityUid uid)
    {
        return false;
    }


    private void GetVerbs(Entity<PotentialPsionicComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        //if (!PotentialPsionicQuery.HasComp(args.Target))
        //    return;

        if (!_adminManager.HasAdminFlag(args.User, AdminFlags.Fun))
            return;

        var target = args.Target;
        if (PsionicQuery.HasComp(target))
        {
            Verb verb = new();
            verb.Text = Loc.GetString("prayer-verbs-remove-psi");
            verb.Message = "Снять псионика";
            verb.Category = VerbCategory.Smite;
            verb.Icon = new SpriteSpecifier.Texture(new("/Textures/Backmen/Interface/VerbIcons/psionic_regeneration.png"));
            verb.Act = () =>
            {
                RemovePsionics(target);
            };
            verb.Impact = LogImpact.High;
            args.Verbs.Add(verb);
        }
        else
        {
            Verb verb = new();
            verb.Text = Loc.GetString("prayer-verbs-psi");
            verb.Message = "Сделать псионика";
            verb.Category = VerbCategory.Tricks;
            verb.Icon = new SpriteSpecifier.Texture(new("/Textures/Backmen/Interface/VerbIcons/psionic_regeneration.png"));
            verb.Act = () =>
            {
                AddPsionics(target);
            };
            verb.Impact = LogImpact.High;
            args.Verbs.Add(verb);
        }
    }



    private void OnMeleeHit(EntityUid uid, AntiPsionicWeaponComponent component, MeleeHitEvent args)
    {
        foreach (var entity in args.HitEntities)
        {
            if (PsionicQuery.HasComp(entity))
            {
                _audio.PlayPvs(Lightburn,entity);
                args.ModifiersList.Add(component.Modifiers);
                if (_random.Prob(component.DisableChance))
                    _statusEffects.TryAddStatusEffect<PsionicsDisabledComponent>(entity, PsionicsDisabled, TimeSpan.FromSeconds(10), true);
            }

            if (UndoMindSwap(entity))
            {
                return;
            }

            if (
                component.Punish &&
                PotentialPsionicQuery.HasComp(entity) &&
                !PsionicQuery.HasComp(entity) &&
                _random.Prob(0.5f)
            )
                _electrocutionSystem.TryDoElectrocution(args.User, null, 20, TimeSpan.FromSeconds(5), false, ignoreInsulation: true);
        }
    }
    private void OnStamHit(EntityUid uid, AntiPsionicWeaponComponent component, StaminaMeleeHitEvent args)
    {
        var bonus = args.HitList.Any(z=>PsionicQuery.HasComp(z.Entity));

        if (!bonus)
            return;

        args.FlatModifier += component.PsychicStaminaDamage;
    }
}
