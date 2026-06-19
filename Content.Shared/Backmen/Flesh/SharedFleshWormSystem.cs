using System.Diagnostics.CodeAnalysis;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.HealthExaminable;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Strip;
using Content.Shared.Verbs;

namespace Content.Shared.Backmen.Flesh;

public abstract partial class SharedFleshWormSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedStrippableSystem _strippable = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FleshWormComponent, GetVerbsEvent<EquipmentVerb>>(OnGetRemoveVerb);
        SubscribeLocalEvent<FleshWormComponent, InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>>>(OnGetRemoveVerbRelayed);
        SubscribeLocalEvent<FleshWormComponent, FleshWormRemoveVerbEvent>(OnRemoveVerb);

        SubscribeLocalEvent<InventoryComponent, ExaminedEvent>(OnWearersExamined);
        SubscribeLocalEvent<InventoryComponent, HealthBeingExaminedEvent>(OnWearersHealthExamined);
    }

    private void OnWearersExamined(Entity<InventoryComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !TryGetFaceWorm(ent, out var worm))
            return;

        args.PushMarkup(Loc.GetString("flesh-worm-wearer-examine", ("ent", worm), ("target", ent)));
    }

    private void OnWearersHealthExamined(Entity<InventoryComponent> ent, ref HealthBeingExaminedEvent args)
    {
        if (!TryGetFaceWorm(ent, out var worm))
            return;

        args.Message.PushNewline();
        args.Message.TryAddMarkup(Loc.GetString("flesh-worm-wearer-examine", ("ent", worm), ("target", ent)), out _);
    }

    private bool TryGetFaceWorm(EntityUid wearer, [NotNullWhen(true)] out EntityUid worm)
    {
        worm = default;

        if (!_inventory.TryGetSlotEntity(wearer, "mask", out var maskUid) || maskUid is not { } mask || !HasComp<FleshWormComponent>(mask))
            return false;

        worm = mask;
        return true;
    }

    private void OnGetRemoveVerbRelayed(Entity<FleshWormComponent> ent, ref InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>> args)
    {
        AddRemoveVerb(ent, ref args.Args);
    }

    private void OnGetRemoveVerb(Entity<FleshWormComponent> ent, ref GetVerbsEvent<EquipmentVerb> args)
    {
        AddRemoveVerb(ent, ref args);
    }

    private void OnRemoveVerb(Entity<FleshWormComponent> ent, ref FleshWormRemoveVerbEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        StartRemoveDoAfter(args.Performer, ent.Owner, args.Wearer, ent.Comp);
    }

    private void AddRemoveVerb(Entity<FleshWormComponent> ent, ref GetVerbsEvent<EquipmentVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !TryGetWearer(ent.Owner, out var wearer))
            return;

        var user = args.User;
        var worm = ent.Owner;
        var comp = ent.Comp;

        if (args.Target != wearer.Value || !CanRemoveWorm(user, worm, wearer.Value, comp))
            return;

        args.Verbs.Add(new EquipmentVerb
        {
            Text = Loc.GetString("flesh-worm-verb-remove"),
            EventTarget = worm,
            ExecutionEventArgs = new FleshWormRemoveVerbEvent { Performer = user, Wearer = wearer.Value },
        });
    }

    protected bool TryGetWearer(EntityUid worm, [NotNullWhen(true)] out EntityUid? wearer)
    {
        wearer = _transform.GetParentUid(worm);

        if (!wearer.Value.IsValid())
            return false;

        return _inventory.TryGetSlotEntity(wearer.Value, "mask", out var mask) && mask == worm;
    }

    protected bool CanRemoveWorm(EntityUid user, EntityUid worm, EntityUid wearer, FleshWormComponent comp)
    {
        if (!TryGetWearer(worm, out var actualWearer) || actualWearer != wearer)
            return false;

        if (!_actionBlocker.CanInteract(user, wearer))
            return false;

        if (!TryComp<StaminaComponent>(wearer, out var stamina))
            return true;

        return stamina.StaminaDamage + comp.SelfRemoveStaminaCost < stamina.CritThreshold && !stamina.Critical;
    }

    protected void StartRemoveDoAfter(EntityUid user, EntityUid worm, EntityUid wearer, FleshWormComponent comp)
    {
        var isSelf = user == wearer;

        if (!CanRemoveWorm(user, worm, wearer, comp))
        {
            if (isSelf)
            {
                _popup.PopupEntity(Loc.GetString("flesh-worm-remove-self-too-tired"),
                    wearer, wearer, PopupType.Medium);
            }

            return;
        }

        if (!isSelf && !_inventory.CanUnequip(user, wearer, "mask", out var reason))
        {
            _popup.PopupCursor(Loc.GetString(reason));
            return;
        }

        var delay = TimeSpan.FromSeconds(comp.RemoveDelay);
        if (isSelf)
            delay *= comp.SelfRemoveDelayMultiplier;

        var (time, stealth) = _strippable.GetStripTimeModifiers(user, wearer, worm, delay);

        var ev = new FleshWormRemoveDoAfterEvent { IsSelf = isSelf };
        var args = new DoAfterArgs(EntityManager, user, time, ev, worm, wearer, worm)
        {
            BreakOnMove = true,
            BreakOnDamage = false,
            NeedHand = !isSelf,
            DistanceThreshold = 2f,
        };

        if (!_doAfter.TryStartDoAfter(args))
            return;

        if (stealth)
            return;

        if (isSelf)
        {
            _popup.PopupEntity(Loc.GetString("flesh-worm-remove-self-start"), wearer, wearer);
            return;
        }

        _popup.PopupEntity(Loc.GetString("flesh-worm-remove-start-user", ("worm", worm)), wearer, user);
        _popup.PopupEntity(
            Loc.GetString("flesh-worm-remove-start-wearer", ("user", Identity.Entity(user, EntityManager))),
            wearer,
            wearer,
            PopupType.Large);
    }

    protected bool HasActiveWormRemoveDoAfter(EntityUid user)
    {
        if (!TryComp<DoAfterComponent>(user, out var doAfter))
            return false;

        foreach (var active in doAfter.DoAfters.Values)
        {
            if (active.Args.Event is FleshWormRemoveDoAfterEvent)
                return true;
        }

        return false;
    }
}

public sealed partial class FleshWormRemoveVerbEvent : HandledEntityEventArgs
{
    public EntityUid Performer;
    public EntityUid Wearer;
}
