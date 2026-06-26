using Content.Shared.Atmos.Rotting;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Overlays;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;
using Content.Shared.Damage.Components;

namespace Content.Client.Overlays;

/// <summary>
/// Shows a healthy icon on mobs.
/// </summary>
public sealed partial class ShowHealthIconsSystem : EquipmentHudSystem<ShowHealthIconsComponent>
{
    [Dependency] private IPrototypeManager _prototypeMan = default!;

    [ViewVariables]
    public HashSet<string> DamageContainers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InjurableComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
        // start-backmen: health-ui
        SubscribeLocalEvent<ConsciousnessComponent, GetStatusIconsEvent>(OnConsciousnessGetStatusIconsEvent);
        // end-backmen: health-ui
        SubscribeLocalEvent<ShowHealthIconsComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<ShowHealthIconsComponent> component)
    {
        base.UpdateInternal(component);

        DamageContainers.Clear();
        foreach (var comp in component.Components)
        {
            foreach (var damageContainerId in comp.DamageContainers)
            {
                DamageContainers.Add(damageContainerId);
            }
        }
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();

        DamageContainers.Clear();
    }

    private void OnHandleState(Entity<ShowHealthIconsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        RefreshOverlay();
    }

    private void OnGetStatusIconsEvent(Entity<InjurableComponent> entity, ref GetStatusIconsEvent args)
    {
        if (!IsActive)
            return;

        args.StatusIcons.AddRange(DecideHealthIcons(entity.Comp.DamageContainer, entity));
    }

    // start-backmen: health-ui
    private void OnConsciousnessGetStatusIconsEvent(Entity<ConsciousnessComponent> entity, ref GetStatusIconsEvent args)
    {
        if (!IsActive)
            return;

        args.StatusIcons.AddRange(DecideHealthIcons(entity.Comp.DamageContainer, entity));
    }
    // end-backmen: health-ui

    private IReadOnlyList<HealthIconPrototype> DecideHealthIcons(
        ProtoId<DamageContainerPrototype>? damageContainer,
        EntityUid entity)
    {
        if (damageContainer == null ||
            !DamageContainers.Contains(damageContainer))
        {
            return Array.Empty<HealthIconPrototype>();
        }

        var result = new List<HealthIconPrototype>();

        if (damageContainer != "Biological")
            return result;

        if (!TryComp<MobStateComponent>(entity, out var state))
            return result;

        ProtoId<HealthIconPrototype>? rottingIcon = null;
        Dictionary<MobState, ProtoId<HealthIconPrototype>>? healthIcons = null;

        if (TryComp<InjurableComponent>(entity, out var injurable))
        {
            rottingIcon = injurable.RottingIcon;
            healthIcons = injurable.HealthIcons;
        }
        else if (TryComp<ConsciousnessComponent>(entity, out var consciousness))
        {
            rottingIcon = consciousness.RottingIcon;
            healthIcons = consciousness.HealthIcons;
        }

        if (healthIcons == null)
            return result;

        if (HasComp<RottingComponent>(entity) && rottingIcon is { } rotting && _prototypeMan.Resolve(rotting, out var rottingProto))
            result.Add(rottingProto);
        else if (healthIcons.TryGetValue(state.CurrentState, out var value) && _prototypeMan.Resolve(value, out var icon))
            result.Add(icon);

        return result;
    }
}
