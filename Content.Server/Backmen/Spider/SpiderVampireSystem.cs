using Content.Shared.Backmen.Spider.Components;
using Content.Server.Actions;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Mobs.Systems;
using Content.Server.Popups;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Content.Shared.Nutrition.AnimalHusbandry;
using Content.Shared.Nutrition.Components;
using Content.Server.Administration.Logs;
using Content.Server.Charges;
using Robust.Shared.Random;
using Content.Server.Backmen.Arachne;
using Content.Server.Backmen.Vampiric;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Backmen.Spider;

public sealed partial class SpiderVampireSystem : EntitySystem
{
    [Dependency] private ActionsSystem _action = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private HungerSystem _hunger = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ChargesSystem _charges = default!;
    [Dependency] private BloodSuckerSystem _bloodSucker = default!;
    [Dependency] private ArachneSystem _arachne = default!;

    private const int DefaultWebExpandRadius = 2;
    private const int DefaultMinWebTiles = 9;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpiderVampireComponent, SpiderVampireEggActionEvent>(OnActionEggUsed);
        SubscribeLocalEvent<SpiderVampireComponent, SpiderVampireEggDoAfterEvent>(OnActionEggUsedAfter);
        SubscribeLocalEvent<SpiderVampireComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, SpiderVampireComponent component, MapInitEvent args)
    {
        _action.AddAction(uid, ref component.SpiderVampireEggAction, component.EggAction);
    }

    private static readonly SoundSpecifier HairballPlay =
        new SoundPathSpecifier("/Audio/Backmen/Effects/Species/hairball.ogg", AudioParams.Default.WithVariation(0.15f));

    private void OnActionEggUsed(EntityUid uid, SpiderVampireComponent component, SpiderVampireEggActionEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<InfantComponent>(uid))
        {
            _popupSystem.PopupEntity("Еще не дорос", uid, uid);
            return;
        }

        if (_mobState.IsCritical(uid) || _mobState.IsDead(uid))
        {
            _popupSystem.PopupEntity("хуйня какая-то", uid, uid);
            return;
        }

        if (TryComp<HungerComponent>(uid, out var hunger) && _hunger.GetHungerThreshold(hunger) < HungerThreshold.Okay)
        {
            _popupSystem.PopupEntity("жрать хочу", uid, uid);
            return;
        }

        if (TryComp<ThirstComponent>(uid, out var thirst) && thirst.CurrentThirstThreshold < ThirstThreshold.Okay)
        {
            _popupSystem.PopupEntity("пить хочу", uid, uid);
            return;
        }

        // LimitedCharges on the action: ActionAttemptEvent already blocks empty;
        // Handled=true below spends a charge via ActionPerformedEvent.
        if (component.SpiderVampireEggAction is not { } eggAction || _charges.IsEmpty(eggAction))
        {
            _popupSystem.PopupEntity(Loc.GetString("spider-vampire-egg-no-charges"), uid, uid);
            _action.SetEnabled(component.SpiderVampireEggAction, false);
            return;
        }

        if (!_doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, uid, component.UsingEggTime,
                new SpiderVampireEggDoAfterEvent(), uid, used: uid)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
            }))
            return;

        _audio.PlayPvs(HairballPlay, uid, AudioParams.Default.WithVariation(0.025f));
        args.Handled = true;
    }

    private void OnActionEggUsedAfter(EntityUid uid, SpiderVampireComponent component,
        SpiderVampireEggDoAfterEvent args)
    {
        if (args.Handled)
            return;

        if (args.Cancelled)
        {
            // Charge was spent when the action was Handled / NPC TryUseCharge — refund.
            if (component.SpiderVampireEggAction is { } cancelledAction)
            {
                _charges.AddCharges(cancelledAction, 1);
                _action.SetCooldown(component.SpiderVampireEggAction, TimeSpan.FromSeconds(1));
                _action.SetEnabled(component.SpiderVampireEggAction, true);
            }

            return;
        }

        var xform = Transform(uid);
        var offspring = Spawn(component.SpawnEgg, xform.Coordinates.Offset(_random.NextVector2(0.3f)));
        _hunger.ModifyHunger(uid, -component.HungerPerBirth);

        if (component.SpiderVampireEggAction is { } action && _charges.IsEmpty(action))
            _action.SetEnabled(component.SpiderVampireEggAction, false);

        _adminLog.Add(LogType.Action, $"{ToPrettyString(uid)} gave birth to {ToPrettyString(offspring)}.");
        _popupSystem.PopupEntity(
            Loc.GetString("reproductive-birth-popup", ("parent", Identity.Entity(uid, EntityManager))), uid);
        args.Handled = true;
    }

    public bool CanLayEgg(EntityUid uid, SpiderVampireComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (component.SpiderVampireEggAction is not { } action || _charges.IsEmpty(action))
            return false;

        if (_mobState.IsCritical(uid) || _mobState.IsDead(uid))
            return false;

        if (TryComp<HungerComponent>(uid, out var hunger) && _hunger.GetHungerThreshold(hunger) < HungerThreshold.Okay)
            return false;

        if (TryComp<ThirstComponent>(uid, out var thirst) && thirst.CurrentThirstThreshold < ThirstThreshold.Okay)
            return false;

        if (!_arachne.IsWebNestReady(Transform(uid).Coordinates, DefaultWebExpandRadius, DefaultMinWebTiles))
            return false;

        return !_bloodSucker.NeedsBlood(uid);
    }

    public bool NPCTryLayEgg(EntityUid uid, SpiderVampireComponent? component = null)
    {
        if (!CanLayEgg(uid, component))
            return false;

        // NPC bypasses PerformAction — spend LimitedCharges the same way ActionPerformed would.
        if (component!.SpiderVampireEggAction is not { } action || !_charges.TryUseCharge(action))
            return false;

        if (!_doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, uid, component.UsingEggTime,
                new SpiderVampireEggDoAfterEvent(), uid, used: uid)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
            }))
        {
            _charges.AddCharges(action, 1);
            return false;
        }

        _audio.PlayPvs(HairballPlay, uid, AudioParams.Default.WithVariation(0.025f));
        return true;
    }
}
