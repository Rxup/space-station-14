using Content.Server.Chat.Systems;
using Content.Server.Heretic.Components;
using Content.Server.Speech.EntitySystems;
using Content.Server.Temperature.Systems;
using Content.Shared.Backmen.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Heretic;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Heretic.EntitySystems;

public sealed partial class MansusGraspSystem : EntitySystem
{
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly RatvarianLanguageSystem _language = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedDoorSystem _door = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;

    public void ApplyGraspEffect(EntityUid performer, EntityUid target, HereticPath path)
    {
        switch (path)
        {
            case HereticPath.Ash:
                var timeSpan = TimeSpan.FromSeconds(5f);
                _statusEffect.TryAddStatusEffect(target, TemporaryBlindnessSystem.BlindingStatusEffect, timeSpan, false, TemporaryBlindnessSystem.BlindingStatusEffect);
                break;

            case HereticPath.Blade:
                // blade is basically an upgrade to the current grasp
                _stamina.TakeStaminaDamage(target, 100f);
                break;

            case HereticPath.Lock:
                // Опен зе доор!
                if (!_doorQuery.TryComp(target, out var door))
                    break;

                if (_doorBoltsQuery.TryComp(target, out var doorBolt))
                    _door.SetBoltsDown((target, doorBolt), false);

                _door.StartOpening(target, door, performer);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/ADT/Heretic/hereticknock.ogg"), target);
                break;

            case HereticPath.Flesh:
                if (_mobsQuery.TryComp(target, out var mobState) && mobState.CurrentState == MobState.Dead)
                {
                    var ghoul = EnsureComp<GhoulComponent>(target);
                    ghoul.BoundHeretic = performer;
                }
                break;

            case HereticPath.Rust:
                // hopefully damage only walls and cyborgs
                _damage.TryChangeDamage(target, new DamageSpecifier() { DamageDict = new Dictionary<string, FixedPoint2>() {{"Structural", 150}}});
                break;

            case HereticPath.Void:
                _temperature.ChangeHeat(target, -30000f, true);
                _statusEffect.TryAddStatusEffect<MutedComponent>(target, "Muted", TimeSpan.FromSeconds(8), false);
                break;

            default:
                return;
        }
    }

    private EntityQuery<HereticComponent> _hereticQuery;
    private EntityQuery<MobStateComponent> _mobsQuery;
    private EntityQuery<DoorComponent> _doorQuery;
    private EntityQuery<DoorBoltComponent> _doorBoltsQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MansusGraspComponent, AfterInteractEvent>(OnAfterInteract);

        SubscribeLocalEvent<TagComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<HereticComponent, DrawRitualRuneDoAfterEvent>(OnRitualRuneDoAfter);

        _hereticQuery = GetEntityQuery<HereticComponent>();
        _mobsQuery = GetEntityQuery<MobStateComponent>();
        _doorQuery = GetEntityQuery<DoorComponent>();
        _doorBoltsQuery = GetEntityQuery<DoorBoltComponent>();
    }

    private void OnAfterInteract(Entity<MansusGraspComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.CanReach)
            return;

        if (args.Target == null || args.Target == args.User)
            return;

        if (!_hereticQuery.TryComp(args.User, out var hereticComp))
        {
            QueueDel(ent);
            return;
        }

        var target = (EntityUid) args.Target;

        if (_hereticQuery.TryComp(args.Target, out var th) && th.CurrentPath == ent.Comp.Path)
            return;

        if (HasComp<StatusEffectsComponent>(target))
        {
            _chat.TrySendInGameICMessage(args.User, Loc.GetString("heretic-speech-mansusgrasp"), InGameICChatType.Speak, false);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Items/welder.ogg"), target);
            _stun.TryKnockdown(target, TimeSpan.FromSeconds(3f), true);
            _stamina.TakeStaminaDamage(target, 65f);
            _language.DoRatvarian(target, TimeSpan.FromSeconds(10f), true);
        }

        // upgraded grasp
        if (hereticComp.CurrentPath != null)
        {
            if (hereticComp.PathStage >= 2)
                ApplyGraspEffect(args.User, target, hereticComp.CurrentPath.Value);

            if (hereticComp.PathStage >= 4 && HasComp<StatusEffectsComponent>(target))
            {
                var markComp = EnsureComp<HereticCombatMarkComponent>(target);
                markComp.Path = hereticComp.CurrentPath.Value;
            }
        }

        hereticComp.MansusGraspActive = false;
        QueueDel(ent);
    }

    [ValidatePrototypeId<EntityPrototype>]
    private const string HereticRuneRitualDrawAnimation = "HereticRuneRitualDrawAnimation";
    [ValidatePrototypeId<TagPrototype>]
    private const string Write = "Write";
    [ValidatePrototypeId<TagPrototype>]
    private const string Pen = "Pen";
    private void OnAfterInteract(Entity<TagComponent> ent, ref AfterInteractEvent args)
    {
        if (!_hereticQuery.TryComp(ent, out var heretic))
            return;
        var tags = ent.Comp.Tags;

        if (!args.CanReach
        || !args.ClickLocation.IsValid(EntityManager)
        || !heretic.MansusGraspActive // no grasp - not special
        || HasComp<ActiveDoAfterComponent>(args.User) // prevent rune shittery
        || !tags.Contains(Write) || !tags.Contains(Pen)) // not a pen
            return;

        // remove our rune if clicked
        if (args.Target != null && HasComp<HereticRitualRuneComponent>(args.Target))
        {
            // todo: add more fluff
            QueueDel(args.Target);
            return;
        }

        // spawn our rune
        var rune = Spawn(HereticRuneRitualDrawAnimation, args.ClickLocation);
        var dargs = new DoAfterArgs(EntityManager, args.User, 14f, new DrawRitualRuneDoAfterEvent(rune, args.ClickLocation), args.User)
        {
            BreakOnDamage = true,
            BreakOnHandChange = true,
            BreakOnMove = true,
            CancelDuplicate = false,
        };
        _doAfter.TryStartDoAfter(dargs);
    }

    [ValidatePrototypeId<EntityPrototype>]
    private const string HereticRuneRitual = "HereticRuneRitual";
    private void OnRitualRuneDoAfter(Entity<HereticComponent> ent, ref DrawRitualRuneDoAfterEvent ev)
    {
        // delete the animation rune regardless
        QueueDel(ev.RitualRune);

        if (!ev.Cancelled)
            Spawn(HereticRuneRitual, ev.Coords);
    }
}
