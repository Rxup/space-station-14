using System.Linq;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Spider;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.TerrorSpider;

public sealed class EggInjectSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;

    private readonly EntProtoId[] _validEggPrototypes =
    [
        "TerrorRedEggSpiderFertilized",
        "TerrorGreenSpiderFertilized",
        "TerrorGrayEggSpiderFertilized"
    ];

    public override void Initialize()
    {
        SubscribeLocalEvent<EggInjectionEvent>(OnEggInjection);
        SubscribeLocalEvent<SpiderComponent, EggInjectionDoAfterEvent>(OnEggInjectionDoAfter);
        SubscribeLocalEvent<EggsLayingEvent>(OnEggsLaying);

        Subs.BuiEvents<TerrorPrincessComponent>(EggsLayingUiKey.Key, subs => subs.Event<EggsLayingBuiMsg>(OnEggsLayingBuiMessage));
    }

    private void OnEggsLaying(EggsLayingEvent ev)
    {
        ev.Handled = true;
        if (TryComp(ev.Performer, out ActorComponent? actor))
        {
            _uiSystem.OpenUi(ev.Performer, EggsLayingUiKey.Key, actor.PlayerSession);
        }
    }

    private void OnEggsLayingBuiMessage(EntityUid uid, TerrorPrincessComponent component, EggsLayingBuiMsg args)
    {
        if (_validEggPrototypes.Contains(args.Egg) && TryComp(uid, out ActorComponent? actor))
        {
            SpawnAtPosition(args.Egg, Transform(uid).Coordinates);
            _uiSystem.CloseUi(uid, EggsLayingUiKey.Key, actor.PlayerSession);
        }
    }

    private void OnEggInjectionDoAfter(Entity<SpiderComponent> ent, ref EggInjectionDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.Target is { } target && !HasComp<HasEggHolderComponent>(target))
        {
            EnsureComp<EggHolderComponent>(target);
            EnsureComp<HasEggHolderComponent>(target);
        }
    }

    private void OnEggInjection(EggInjectionEvent ev)
    {
        if (HasComp<HasEggHolderComponent>(ev.Target))
        {
            _popup.PopupEntity("The target already contains eggs.", ev.Performer);
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, ev.Performer, TimeSpan.FromSeconds(6), new EggInjectionDoAfterEvent(), ev.Performer, ev.Target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 1f
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }
}
