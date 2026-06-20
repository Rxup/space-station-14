using System.Linq;
using Content.Server.Body.Systems;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Pain.Systems;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Backmen.Body.Systems;
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
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Tourniquet;

/// <summary>
/// This handles tourniqueting people
/// </summary>
public sealed partial class TourniquetSystem : EntitySystem
{
    [Dependency] private BkmBodySharedSystem _body = default!;
    [Dependency] private WoundSystem _wound = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private PainSystem _pain = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;

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
        if (!TryComp<TargetingComponent>(user, out var targeting))
            return false;

        // To prevent people from tourniqueting simple mobs
        if (!HasComp<BodyComponent>(target) || !HasComp<ConsciousnessComponent>(target))
            return false;

        var (partType, _) = _body.ConvertTargetBodyPart(targeting.Target);
        if (tourniquet.BlockedBodyParts.Contains(partType))
        {
            _popup.PopupEntity(Loc.GetString("cant-put-tourniquet-here"), target, PopupType.MediumCaution);
            return false;
        }

        _popup.PopupEntity(Loc.GetString("puts-on-a-tourniquet", ("user", user), ("target", target)), target, PopupType.Medium);
        _audio.PlayPvs(tourniquet.TourniquetPutOnSound, target, AudioParams.Default.WithVariation(0.125f).WithVolume(1f));

        var doAfterEventArgs =
            new DoAfterArgs(EntityManager, user, tourniquet.Delay, new TourniquetDoAfterEvent(), target, target: target, used: tourniquetEnt)
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
        _popup.PopupEntity(Loc.GetString("takes-off-a-tourniquet", ("user", user), ("part", tourniquet.BodyPartTourniqueted!)), target, PopupType.Medium);
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

        if (!TryComp<TargetingComponent>(args.User, out var targeting))
            return;

        var container = _container.EnsureContainer<ContainerSlot>(args.Target!.Value, TourniquetContainerId);
        if (container.ContainedEntity.HasValue)
        {
            _popup.PopupEntity(Loc.GetString("already-tourniqueted"), ent, PopupType.Medium);
            return;
        }

        var (partType, symmetry) = _body.ConvertTargetBodyPart(targeting.Target);

        EntityUid? targetPart = null;
        if (_body.TryGetWoundableTargetByType(ent, partType, symmetry, out var woundableTarget))
            targetPart = woundableTarget;

        if (targetPart == null)
        {
            _popup.PopupEntity(Loc.GetString("does-not-exist-rebell"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (!_container.Insert(args.Used.Value, container))
        {
            _popup.PopupEntity(Loc.GetString("cant-tourniquet"), ent, PopupType.Medium);
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
