using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.DoAfter;
using Content.Server.Flash;
using Content.Server.Hands.Systems;
using Content.Server.Magic;
using Content.Server.Polymorph.Systems;
using Content.Server.Popups;
using Content.Server.Radio.Components;
using Content.Server.Store.Systems;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Heretic;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Store.Components;
using Robust.Shared.Audio.Systems;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Content.Shared.Body.Systems;
using Content.Server.Medical;
using Content.Shared.Backmen.Chat;
using Content.Shared.Radio;
using Robust.Server.GameObjects;
using Content.Shared.Stunnable;
using Robust.Shared.Map;
using Content.Shared.StatusEffect;
using Content.Shared.Throwing;
using Robust.Shared.Prototypes;

namespace Content.Server.Heretic.Abilities;

public sealed partial class HereticAbilitySystem : SharedHereticAbilitySystem
{
    // keeping track of all systems in a single file
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly PolymorphSystem _poly = default!;
    [Dependency] private readonly ChainFireballSystem _splitball = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobstate = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly DamageableSystem _dmg = default!;
    [Dependency] private readonly StaminaSystem _stam = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedAudioSystem _aud = default!;
    [Dependency] private readonly DoAfterSystem _doafter = default!;
    [Dependency] private readonly FlashSystem _flash = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly VomitSystem _vomit = default!;
    [Dependency] private readonly PhysicsSystem _phys = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly ThrowingSystem _throw = default!;



    private List<EntityUid> GetNearbyPeople(Entity<HereticComponent> ent, float range)
    {
        var list = new List<EntityUid>();
        var lookup = _lookup.GetEntitiesInRange<StatusEffectsComponent>(Transform(ent).Coordinates, range);

        foreach (var look in lookup)
        {
            // ignore heretics with the same path*, affect everyone else
            if ((_hereticQuery.TryComp(look, out var th) && th.CurrentPath == ent.Comp.CurrentPath)
            || _ghoulQuery.HasComp(look))
                continue;

            list.Add(look);
        }
        return list;
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticMagicItemComponent, InventoryRelayedEvent<CheckMagicItemEvent>>(OnCheckMagicItem);

        SubscribeLocalEvent<HereticComponent, EventHereticOpenStore>(OnStore);
        SubscribeLocalEvent<HereticComponent, EventHereticMansusGrasp>(OnMansusGrasp);

        SubscribeLocalEvent<GhoulComponent, EventHereticMansusLink>(OnMansusLink);
        SubscribeLocalEvent<GhoulComponent, HereticMansusLinkDoAfter>(OnMansusLinkDoafter);

        SubscribeAsh();
        SubscribeFlesh();
        SubscribeVoid();
    }

    protected override void TrySendInGameMessage(HereticActionComponent comp, EntityUid ent)
    {
        if (!string.IsNullOrWhiteSpace(comp.MessageLoc))
            _chat.TrySendInGameICMessage(ent, Loc.GetString(comp.MessageLoc!), InGameICChatType.Speak, false);
    }

    private void OnCheckMagicItem(Entity<HereticMagicItemComponent> ent, ref InventoryRelayedEvent<CheckMagicItemEvent> args)
    {
        // no need to check fo anythign because the event gets processed only by magic items
        args.Args.Handled = true;
    }

    private void OnStore(EntityUid uid, HereticComponent component, ref EventHereticOpenStore args)
    {
        if (!TryComp<StoreComponent>(uid, out var store))
            return;
        _store.ToggleUi(uid, uid, store);
    }

    [ValidatePrototypeId<EntityPrototype>]
    private const string TouchSpellMansus = "TouchSpellMansus";
    private void OnMansusGrasp(Entity<HereticComponent> ent, ref EventHereticMansusGrasp args)
    {
        if (ent.Comp.MansusGraspActive)
        {
            _popup.PopupEntity(Loc.GetString("heretic-ability-fail"), ent, ent);
            return;
        }

        var st = Spawn(TouchSpellMansus, Transform(ent).Coordinates);

        if (!_hands.TryForcePickupAnyHand(ent, st))
        {
            _popup.PopupEntity(Loc.GetString("heretic-ability-fail"), ent, ent);
            QueueDel(st);
            return;
        }

        ent.Comp.MansusGraspActive = true;
        args.Handled = true;
    }

    [ValidatePrototypeId<RadioChannelPrototype>]
    private const string Mansus = "Mansus";
    private void OnMansusLink(Entity<GhoulComponent> ent, ref EventHereticMansusLink args)
    {
        if (!HasComp<MindContainerComponent>(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("heretic-manselink-fail-nomind"), ent, ent);
            return;
        }

        if (TryComp<ActiveRadioComponent>(args.Target, out var radio)
        && radio.Channels.Contains(Mansus))
        {
            _popup.PopupEntity(Loc.GetString("heretic-manselink-fail-exists"), ent, ent);
            return;
        }

        var dargs = new DoAfterArgs(EntityManager, ent, 5f, new HereticMansusLinkDoAfter(args.Target), ent, args.Target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnWeightlessMove = true,
        };
        _popup.PopupEntity(Loc.GetString("heretic-manselink-start"), ent, ent);
        _popup.PopupEntity(Loc.GetString("heretic-manselink-start-target"), args.Target, args.Target, PopupType.MediumCaution);
        _doafter.TryStartDoAfter(dargs);
    }
    private void OnMansusLinkDoafter(Entity<GhoulComponent> ent, ref HereticMansusLinkDoAfter args)
    {
        // var reciever = EnsureComp<IntrinsicRadioReceiverComponent>(args.Target);
        var transmitter = EnsureComp<IntrinsicRadioTransmitterComponent>(args.Target);
        var radio = EnsureComp<ActiveRadioComponent>(args.Target);
        radio.Channels = [Mansus];
        transmitter.Channels = [Mansus];

        // this "* 1000f" (divided by 1000 in FlashSystem) is gonna age like fine wine :clueless:
        _flash.Flash(args.Target, null, null, 2f * 1000f, 0f, false, true, stunDuration: TimeSpan.FromSeconds(1f));
    }
}
