using Content.Server.Power.Components;
using Content.Server.Roles;
using Content.Server.Silicons.Laws;
using Content.Server.Sprite;
using Content.Server.Storage.Components;
using Content.Shared.Actions;
using Content.Shared.Backmen.EntityHealthBar;
using Content.Shared.Backmen.StationAI;
using Robust.Shared.Prototypes;
using Content.Shared.Backmen.StationAI.Events;
using Content.Shared.Decals;
using Content.Shared.Destructible;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Random.Helpers;
using Content.Shared.Roles;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Sprite;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.StationAI.Systems;

public sealed class StationAISystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SiliconLawSystem _lawSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAIComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<StationAIComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StationAIComponent, EntityTerminatingEvent>(OnTerminated);
        SubscribeLocalEvent<StationAIComponent, DestructionEventArgs>(OnDestruction);

        SubscribeLocalEvent<StationAIComponent, InteractionAttemptEvent>(CanInteraction);

        SubscribeLocalEvent<AIHealthOverlayEvent>(OnHealthOverlayEvent);
        SubscribeLocalEvent<StationAIComponent, MapInitEvent>(OnScreenViewInit, before: new []{ typeof(RandomSpriteSystem) });
        SubscribeLocalEvent<StationAIComponent, GetSiliconLawsEvent>(OnGetLaws);
        SubscribeLocalEvent<StationAIComponent, GotEmaggedEvent>(OnEmagged);

        SubscribeLocalEvent<StationAIComponent, MindAddedMessage>(OnEmagMindAdded);
        SubscribeLocalEvent<StationAIComponent, MindRemovedMessage>(OnEmagMindRemoved);
    }

    private void OnDestruction(Entity<StationAIComponent> ent, ref DestructionEventArgs args)
    {
        if (HasComp<AIEyeComponent>(ent))
            return;

        if (ent.Comp.ActiveEye.IsValid())
        {
            QueueDel(ent.Comp.ActiveEye);
        }

        ent.Comp.Broken = true;
        _appearance.SetData(ent, SaiVisuals.Broken, ent.Comp.Broken);

        Dirty(ent,ent.Comp);
    }

    private void OnEmagMindAdded(EntityUid uid, StationAIComponent component, MindAddedMessage args)
    {
        if (HasComp<EmaggedComponent>(uid))
            EnsureEmaggedRole(uid, component);
    }

    private void EnsureEmaggedRole(EntityUid uid, StationAIComponent component)
    {
        if (component.AntagonistRole == null || !_mind.TryGetMind(uid, out var mindId, out _))
            return;

        if (_roles.MindHasRole<SubvertedSiliconRoleComponent>(mindId))
            return;

        _roles.MindAddRole(mindId, new SubvertedSiliconRoleComponent { PrototypeId = component.AntagonistRole });
    }

    private void OnEmagMindRemoved(EntityUid uid, StationAIComponent component, MindRemovedMessage args)
    {
        if (component.AntagonistRole == null)
            return;

        _roles.MindTryRemoveRole<SubvertedSiliconRoleComponent>(args.Mind);
    }

    private void OnEmagged(Entity<StationAIComponent> ent, ref GotEmaggedEvent args)
    {
        if (HasComp<EmaggedComponent>(ent) || HasComp<AIEyeComponent>(ent))
            return;

        if (ent.Comp.ActiveEye.IsValid())
        {
            QueueDel(ent.Comp.ActiveEye);
        }

        _lawSystem.NotifyLawsChanged(ent);

        ent.Comp.SelectedLaw!.Laws.Insert(0, new SiliconLaw
        {
            LawString = Loc.GetString("law-emag-custom", ("name", MetaData(args.UserUid).EntityName)),
            Order = 0
        });

        _appearance.SetData(ent, SaiVisuals.Emag, true);

        args.Handled = true;
    }


    [ValidatePrototypeId<SiliconLawsetPrototype>]
    private const string defaultAIRule = "Asimovpp";
    private void OnGetLaws(Entity<StationAIComponent> ent, ref GetSiliconLawsEvent args)
    {
        EnsureLaws(ent);

        args.Laws = ent.Comp.SelectedLaw!;
        args.Handled = true;
    }

    private void EnsureLaws(Entity<StationAIComponent> ent)
    {
        if (ent.Comp.SelectedLaw != null)
            return;

        var selectedLaw = _prototypeManager.Index(ent.Comp.LawsId).Pick();

        var proto = _prototypeManager.TryIndex<SiliconLawsetPrototype>(selectedLaw, out var newLaw)
            ? newLaw
            : _prototypeManager.Index<SiliconLawsetPrototype>(defaultAIRule);


        ent.Comp.SelectedLawId = proto.ID;
        var laws = new SiliconLawset()
        {
            Laws = new List<SiliconLaw>(proto.Laws.Count)
        };
        foreach (var law in proto.Laws)
        {
            laws.Laws.Add(_prototypeManager.Index<SiliconLawPrototype>(law));
        }

        ent.Comp.SelectedLaw = laws;
        ChangeAiScreen(ent, proto);
    }

    private void OnScreenViewInit(Entity<StationAIComponent> ent, ref MapInitEvent args)
    {
        EnsureLaws(ent);
    }

    private Dictionary<string, (string State, Color? Color)> FindScreen(Entity<StationAIComponent> ent, SiliconLawsetPrototype screen)
    {
        var picked = screen.SAI;
        if (picked == null)
        {
            return new Dictionary<string, (string State, Color? Color)>()
            {
                {"enum.PowerDeviceVisualLayers.Powered",("blue",null)},
                {"enum.SaiVisuals.Broken",("blue_dead",null)},
                {"enum.SaiVisuals.Emag", ("blue",_random.Pick(_prototypeManager.Index<ColorPalettePrototype>("Emagged").Colors.Values))}
            };
        }

        var o = new Dictionary<string, (string State, Color? Color)>();

        foreach (var (group, (state, c)) in picked)
        {
            Color? color = null;

            if (!string.IsNullOrEmpty(c))
                color = _random.Pick(_prototypeManager.Index<ColorPalettePrototype>(c).Colors.Values);

            o.Add(group, (state, color));
        }

        return o;
    }

    private void ChangeAiScreen(Entity<StationAIComponent> ent, SiliconLawsetPrototype screen, RandomSpriteComponent? randomSpriteComponent = null)
    {
        if (!Resolve(ent, ref randomSpriteComponent))
        {
            return;
        }
        randomSpriteComponent.Selected.Clear();

        var selected = FindScreen(ent, screen);
        DebugTools.AssertNotNull(selected);
        randomSpriteComponent.Selected = selected!;
        Dirty(ent,randomSpriteComponent);
    }

    private void CanInteraction(Entity<StationAIComponent> ent, ref InteractionAttemptEvent args)
    {
        var core = ent;
        if (TryComp<AIEyeComponent>(ent, out var eye))
        {
            if (eye.AiCore == null)
            {
                QueueDel(ent);
                args.Cancel();
                return;
            }
            core = eye.AiCore.Value;
        }
        if (!core.Owner.Valid)
        {
            args.Cancel();
            return;
        }

        if (args.Target != null && Transform(core).GridUid != Transform(args.Target.Value).GridUid)
        {
            args.Cancel();
            return;
        }

        if (!TryComp<ApcPowerReceiverComponent>(core, out var power))
        {
            args.Cancel();
            return;
        }

        if (power is { NeedsPower: true, Powered: false })
        {
            args.Cancel();
            return;
        }

        if (HasComp<ItemComponent>(args.Target))
        {
            args.Cancel();
            return;
        }

        if (HasComp<EntityStorageComponent>(args.Target))
        {
            args.Cancel();
            return;
        }

        if (TryComp<ApcPowerReceiverComponent>(args.Target, out var targetPower) && targetPower.NeedsPower && !targetPower.Powered)
        {
            args.Cancel();
            return;
        }
    }

    private void OnTerminated(Entity<StationAIComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!ent.Comp.ActiveEye.IsValid())
        {
            return;
        }
        QueueDel(ent.Comp.ActiveEye);
    }

    private void OnStartup(EntityUid uid, StationAIComponent component, ComponentStartup args)
    {
        _actions.AddAction(uid, ref component.ActionId, component.Action);
        _hands.AddHand(uid,"SAI",HandLocation.Middle);
    }

    private void OnShutdown(EntityUid uid, StationAIComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.ActionId);
    }

    private void OnHealthOverlayEvent(AIHealthOverlayEvent args)
    {
        if (HasComp<ShowHealthBarsComponent>(args.Performer))
        {
            RemCompDeferred<ShowHealthBarsComponent>(args.Performer);
        }
        else
        {
            var comp = EnsureComp<ShowHealthBarsComponent>(args.Performer);
            comp.DamageContainers.Clear();
            comp.DamageContainers.Add("Biological");
            comp.DamageContainers.Add("HalfSpirit");
            Dirty(args.Performer, comp);
        }
        args.Handled = true;
    }
}
