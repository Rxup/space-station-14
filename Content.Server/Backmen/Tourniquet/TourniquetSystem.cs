using Content.Server.Body.Systems;
using Content.Shared.Backmen.Medical;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Pain.Systems;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Tourniquet;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Server.Backmen.Tourniquet;

/// <summary>
/// This handles tourniqueting people
/// </summary>
public sealed partial class TourniquetSystem : EntitySystem
{
    [Dependency] private WoundSystem _wound = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private PainSystem _pain = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private BackmenMedicalTargetSystem _medicalTarget = default!;

    private const string TourniquetContainerId = "Tourniquet";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TourniquetComponent, UseInHandEvent>(OnTourniquetUse);
        SubscribeLocalEvent<TourniquetComponent, AfterInteractEvent>(OnTourniquetAfterInteract);

        SubscribeLocalEvent<BodyComponent, TourniquetDoAfterEvent>(OnBodyDoAfter);
        SubscribeLocalEvent<BodyComponent, RemoveTourniquetDoAfterEvent>(OnTourniquetTakenOff);

        SubscribeLocalEvent<BodyComponent, GetVerbsEvent<InnateVerb>>(OnBodyGetVerbs);
    }

    private bool TryTourniquet(EntityUid target, EntityUid user, EntityUid tourniquetEnt, TourniquetComponent tourniquet)
    {
        if (!TryComp<TargetingComponent>(user, out _))
            return false;

        // To prevent people from tourniqueting simple mobs
        if (!HasComp<BodyComponent>(target) || !HasComp<ConsciousnessComponent>(target))
            return false;

        // start-backmen: medical-targeting
        if (!_medicalTarget.TryResolveTourniquetTarget(target, user, tourniquet.BlockedBodyParts, out var targetPart))
        {
            _popup.PopupEntity(Loc.GetString("medical-item-no-healable-damage", ("target", target)), target, user, PopupType.MediumCaution);
            return false;
        }

        _audio.PlayPvs(tourniquet.TourniquetPutOnSound, target, AudioParams.Default.WithVariation(0.125f).WithVolume(1f));

        var doAfterEvent = new TourniquetDoAfterEvent
        {
            TargetWoundable = GetNetEntity(targetPart),
        };

        var doAfterEventArgs =
            new DoAfterArgs(EntityManager, user, tourniquet.Delay, doAfterEvent, target, target: target, used: tourniquetEnt)
        // end-backmen: medical-targeting
            {
                BreakOnDamage = true,
                NeedHand = true,
                BreakOnMove = true,
                BreakOnWeightlessMove = false,
            };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
        return true;
    }

    private void TakeOffTourniquet(EntityUid target, EntityUid user, EntityUid tourniquetEnt, TourniquetComponent tourniquet)
    {
        _audio.PlayPvs(tourniquet.TourniquetPutOffSound, target, AudioParams.Default.WithVariation(0.125f).WithVolume(1f));

        var doAfterEventArgs =
            new DoAfterArgs(EntityManager, user, tourniquet.RemoveDelay, new RemoveTourniquetDoAfterEvent(), target, target: target, used: tourniquetEnt)
            {
                BreakOnDamage = true,
                NeedHand = true,
                BreakOnMove = true,
                BreakOnWeightlessMove = false,
            };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }

    private void OnTourniquetUse(Entity<TourniquetComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (TryTourniquet(args.User, args.User, ent, ent))
            args.Handled = true;
    }

    private void OnTourniquetAfterInteract(Entity<TourniquetComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        if (TryTourniquet(args.Target.Value, args.User, ent, ent))
            args.Handled = true;
    }

    private void OnBodyDoAfter(EntityUid ent, BodyComponent comp, ref TourniquetDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (!TryComp<TourniquetComponent>(args.Used, out var tourniquet))
            return;

        var container = _container.EnsureContainer<ContainerSlot>(args.Target!.Value, TourniquetContainerId);
        if (container.ContainedEntity.HasValue)
        {
            _popup.PopupEntity(Loc.GetString("already-tourniqueted"), ent, PopupType.Medium);
            args.Handled = true;
            return;
        }

        // start-backmen: medical-targeting
        if (!TryGetEntity(args.TargetWoundable, out var targetPart) || targetPart == null)
        {
            _popup.PopupEntity(Loc.GetString("does-not-exist-rebell"), ent, args.User, PopupType.MediumCaution);
            args.Handled = true;
            return;
        }
        // end-backmen: medical-targeting

        if (!_container.Insert(args.Used.Value, container))
        {
            _popup.PopupEntity(Loc.GetString("cant-tourniquet"), ent, PopupType.Medium);
            args.Handled = true;
            return;
        }

        _pain.TryAddPainFeelsModifier(args.Used.Value, "Tourniquet", targetPart.Value, -10f);
        _bloodstream.TryAddBleedModifier(targetPart.Value, "TourniquetPresent", 100, false, true);

        foreach (var childWoundable in _wound.GetAllWoundableChildren(targetPart.Value))
        {
            _pain.TryAddPainFeelsModifier(args.Used.Value, "Tourniquet", childWoundable, -10f);
            _bloodstream.TryAddBleedModifier(childWoundable, "TourniquetPresent", 100, false, true, childWoundable);
        }

        tourniquet.BodyPartTourniqueted = targetPart.Value;

        _popup.PopupEntity(Loc.GetString("puts-on-a-tourniquet", ("user", args.User), ("target", ent)), ent, PopupType.Medium);

        args.Handled = true;
    }

    private void OnTourniquetTakenOff(Entity<BodyComponent> ent, ref RemoveTourniquetDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (!TryComp<TourniquetComponent>(args.Used, out var tourniquet))
            return;

        if (!_container.TryGetContainer(ent, TourniquetContainerId, out var container))
            return;

        var tourniquetedBodyPart = tourniquet.BodyPartTourniqueted;
        if (tourniquetedBodyPart == null)
            return;

        var isSeveredLimbTourniquet = TryComp<BodyPartComponent>(tourniquetedBodyPart.Value, out var bodyPartComp)
            && tourniquet.BlockedBodyParts.Contains(bodyPartComp.PartType);

        if (isSeveredLimbTourniquet)
        {
            foreach (var woundEnt in _wound.GetWoundableWounds(tourniquetedBodyPart.Value))
            {
                if (!TryComp<BleedInflicterComponent>(woundEnt, out var bleedInflicter))
                    continue;

                if (!TryComp<TourniquetableComponent>(woundEnt, out var tourniquetableComp))
                    continue;

                if (tourniquetableComp.CurrentTourniquetEntity != args.Used)
                    continue;

                tourniquetableComp.CurrentTourniquetEntity = null;
                _bloodstream.TryRemoveBleedModifier(woundEnt, "TourniquetPresent", bleedInflicter);
            }
        }
        else
        {
            _pain.TryRemovePainFeelsModifier(args.Used.Value, "Tourniquet", tourniquetedBodyPart.Value);
            _bloodstream.TryRemoveBleedModifier(tourniquetedBodyPart.Value, "TourniquetPresent", true);

            foreach (var woundable in _wound.GetAllWoundableChildren(tourniquetedBodyPart.Value))
            {
                _pain.TryRemovePainFeelsModifier(args.Used.Value, "Tourniquet", woundable);
                _bloodstream.TryRemoveBleedModifier(woundable, "TourniquetPresent", true, woundable);
            }
        }

        _container.Remove(args.Used.Value, container);

        _hands.TryPickupAnyHand(args.User, args.Used.Value);
        tourniquet.BodyPartTourniqueted = null;

        _popup.PopupEntity(Loc.GetString("takes-off-a-tourniquet", ("user", args.User), ("part", tourniquetedBodyPart.Value)), ent, PopupType.Medium);

        args.Handled = true;
    }

    private void OnBodyGetVerbs(EntityUid ent, BodyComponent comp, GetVerbsEvent<InnateVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!_container.TryGetContainer(args.Target, TourniquetContainerId, out var container))
            return;

        foreach (var entity in container.ContainedEntities)
        {
            var tourniquet = Comp<TourniquetComponent>(entity);
            InnateVerb verb = new()
            {
                Act = () => TakeOffTourniquet(args.Target, args.User, entity, tourniquet),
                Text = Loc.GetString("take-off-tourniquet", ("part", tourniquet.BodyPartTourniqueted!)),
                // Icon = new SpriteSpecifier.Texture(new ("/Textures/")),
                Priority = 2
            };
            args.Verbs.Add(verb);
        }
    }
}
