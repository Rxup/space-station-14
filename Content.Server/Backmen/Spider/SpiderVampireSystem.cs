using Content.Shared.Backmen.Spider.Components;
using Robust.Shared.Prototypes;
using Content.Server.Actions;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Mobs.Systems;
using Content.Server.Popups;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Content.Shared.Nutrition.AnimalHusbandry;
using Content.Shared.Nutrition.Components;
using Content.Server.Administration.Logs;
using Robust.Shared.Random;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Spider;

public sealed class SpiderVampireSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _action = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpiderVampireComponent, SpiderVampireEggActionEvent>(OnActionEggUsed);
        SubscribeLocalEvent<SpiderVampireComponent, SpiderVampireEggDoAfterEvent>(OnActionEggUsedAfter);

        SubscribeLocalEvent<SpiderVampireComponent, MapInitEvent>(OnMapInit);
    }

    #region Добавить скилл

    [ValidatePrototypeId<EntityPrototype>] private const string SpiderVampireEggAction = "ActionSpiderVampireEgg";

    private void OnMapInit(EntityUid uid, SpiderVampireComponent component, MapInitEvent args)
    {
        _action.AddAction(uid, ref component.SpiderVampireEggAction, SpiderVampireEggAction);
        //_action.SetCooldown(component.SpiderVampireEggAction, _gameTiming.CurTime,
        //    _gameTiming.CurTime + (TimeSpan) component.InitCooldown);
        _action.SetCharges(component.SpiderVampireEggAction, component.Charges);
    }

    #endregion

    #region Нажали на кнопку

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

        if (_mobState.IsIncapacitated(uid))
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

        _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, uid, component.UsingEggTime,
            new SpiderVampireEggDoAfterEvent(), uid, used: uid)
        {
            BreakOnUserMove = true,
            BreakOnDamage = true,
        });

        _audio.PlayPvs(HairballPlay, uid,
            AudioParams.Default.WithVariation(0.025f));
        args.Handled = true;
    }

    #endregion

    #region После каста

    private void OnActionEggUsedAfter(EntityUid uid, SpiderVampireComponent component,
        SpiderVampireEggDoAfterEvent args)
    {
        if (args.Handled)
            return;
        if (args.Cancelled)
        {
            if (_action.TryGetActionData(component.SpiderVampireEggAction, out var data))
            {
                _action.SetCharges(component.SpiderVampireEggAction, data.Charges+1);
                _action.SetCooldown(component.SpiderVampireEggAction, _gameTiming.CurTime,
                    _gameTiming.CurTime + TimeSpan.FromSeconds(1));
                _action.SetEnabled(component.SpiderVampireEggAction, true);
            }
            return;
        }

        var xform = Transform(uid);
        var offspring = Spawn(component.SpawnEgg, xform.Coordinates.Offset(_random.NextVector2(0.3f)));
        _hunger.ModifyHunger(uid, -component.HungerPerBirth);
        _adminLog.Add(LogType.Action, $"{ToPrettyString(uid)} gave birth to {ToPrettyString(offspring)}.");
        _popupSystem.PopupEntity(
            Loc.GetString("reproductive-birth-popup", ("parent", Identity.Entity(uid, EntityManager))), uid);
    }

    #endregion
}
