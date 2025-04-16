using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Surgery.Traumas.Systems;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Backmen.Surgery.Wounds.Systems;

public sealed partial class WoundSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;

    [Dependency] private readonly IRobustRandom _random = default!;

    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    // I'm the one.... who throws........
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly TraumaSystem _trauma = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("wounds");

        SubscribeLocalEvent<WoundComponent, ComponentGetState>(OnComponentGet);
        SubscribeLocalEvent<WoundComponent, ComponentHandleState>(OnComponentHandleState);

        InitWounding();
    }

    private void OnComponentGet(EntityUid uid, WoundComponent comp, ref ComponentGetState args)
    {
        var state = new WoundComponentState
        {
            HoldingWoundable = GetNetEntity(comp.HoldingWoundable),

            WoundSeverityPoint = comp.WoundSeverityPoint,
            WoundableIntegrityMultiplier = comp.WoundableIntegrityMultiplier,

            WoundType = comp.WoundType,

            DamageGroup = comp.DamageGroup,
            DamageType = comp.DamageType,

            ScarWound = comp.ScarWound,
            IsScar = comp.IsScar,

            WoundSeverity = comp.WoundSeverity,

            WoundVisibility = comp.WoundVisibility,

            CanBeHealed = comp.CanBeHealed,
        };

        args.State = state;
    }

    private void OnComponentHandleState(EntityUid uid, WoundComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not WoundComponentState state)
            return;

        // Predict events on client!!
        var holdingWoundable = GetEntity(state.HoldingWoundable);
        if (holdingWoundable != component.HoldingWoundable)
        {
            if (holdingWoundable == EntityUid.Invalid)
            {
                if (TryComp(holdingWoundable, out WoundableComponent? oldParentWoundable) &&
                    TryComp(oldParentWoundable.RootWoundable, out WoundableComponent? oldWoundableRoot))
                {
                    var ev2 = new WoundRemovedEvent(component, oldParentWoundable, oldWoundableRoot);
                    RaiseLocalEvent(holdingWoundable, ref ev2);
                }
            }
            else
            {
                var parentWoundable = Comp<WoundableComponent>(holdingWoundable);
                var woundableRoot = Comp<WoundableComponent>(parentWoundable.RootWoundable);

                var ev = new WoundAddedEvent(component, parentWoundable, woundableRoot);
                RaiseLocalEvent(uid, ref ev);

                var ev1 = new WoundAddedEvent(component, parentWoundable, woundableRoot);
                RaiseLocalEvent(holdingWoundable, ref ev1);

                var bodyPart = Comp<BodyPartComponent>(holdingWoundable);
                if (bodyPart.Body.HasValue)
                {
                    var ev2 = new WoundAddedOnBodyEvent(uid, component, parentWoundable, woundableRoot);
                    RaiseLocalEvent(bodyPart.Body.Value, ref ev2);
                }
            }
        }
        component.HoldingWoundable = holdingWoundable;

        if (component.WoundSeverityPoint != state.WoundSeverityPoint)
        {
            var ev = new WoundSeverityPointChangedEvent(component, component.WoundSeverityPoint, state.WoundSeverityPoint);
            RaiseLocalEvent(uid, ref ev);

            // TODO: On body changed events aren't predicted, welp
        }

        component.WoundSeverityPoint = state.WoundSeverityPoint;
        component.WoundableIntegrityMultiplier = state.WoundableIntegrityMultiplier;

        if (component.HoldingWoundable != EntityUid.Invalid)
        {
            UpdateWoundableIntegrity(component.HoldingWoundable);
            CheckWoundableSeverityThresholds(component.HoldingWoundable);
        }

        component.WoundType = state.WoundType;

        component.DamageGroup = state.DamageGroup;
        if (state.DamageType != null)
            component.DamageType = state.DamageType;

        component.ScarWound = state.ScarWound;
        component.IsScar = state.IsScar;

        component.WoundVisibility = state.WoundVisibility;
        component.CanBeHealed = state.CanBeHealed;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _woundableJobQueue.Process();

        if (!_timing.IsFirstTimePredicted)
            return;

        var timeToHeal = 1 / _cfg.GetCVar(CCVars.MedicalHealingTickrate);
        using var query = EntityQueryEnumerator<WoundableComponent>();
        while (query.MoveNext(out var ent, out var woundable))
        {
            woundable.HealingRateAccumulated += frameTime;
            if (woundable.HealingRateAccumulated < timeToHeal)
                continue;

            woundable.HealingRateAccumulated -= timeToHeal;
            _woundableJobQueue.EnqueueJob(new IntegrityJob(this, (ent, woundable), WoundableJobTime));
        }
    }
}
