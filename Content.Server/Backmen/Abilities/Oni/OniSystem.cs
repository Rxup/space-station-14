using Content.Server.Tools;
using Content.Shared.Tools.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;

namespace Content.Server.Backmen.Abilities.Oni;

public sealed class OniSystem : EntitySystem
{
    [Dependency] private readonly ToolSystem _toolSystem = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OniComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<OniComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
        SubscribeLocalEvent<OniComponent, MeleeHitEvent>(OnOniMeleeHit);
        SubscribeLocalEvent<HeldByOniComponent, MeleeHitEvent>(OnHeldMeleeHit);
        SubscribeLocalEvent<HeldByOniComponent, StaminaMeleeHitEvent>(OnStamHit);
        SubscribeLocalEvent<HeldByOniComponent, GunRefreshModifiersEvent>(OnGunUpdate);
    }

    private void OnGunUpdate(Entity<HeldByOniComponent> ent, ref GunRefreshModifiersEvent args)
    {
        args.MaxAngle *= 15f;
        args.AngleIncrease *= 15f;
        args.MaxAngle *= 15f;
    }

    private void OnEntInserted(EntityUid uid, OniComponent component, EntInsertedIntoContainerMessage args)
    {
        var heldComp = EnsureComp<HeldByOniComponent>(args.Entity);
        heldComp.Holder = uid;

        if (TryComp<ToolComponent>(args.Entity, out var tool) && _toolSystem.HasQuality(args.Entity, "Prying", tool))
            tool.SpeedModifier *= 1.66f;

        if (TryComp<GunComponent>(args.Entity, out var gun))
        {
            _gun.RefreshModifiers((args.Entity,gun));
        }
    }

    private void OnEntRemoved(EntityUid uid, OniComponent component, EntRemovedFromContainerMessage args)
    {
        if (TryComp<ToolComponent>(args.Entity, out var tool) && _toolSystem.HasQuality(args.Entity, "Prying", tool))
            tool.SpeedModifier /= 1.66f;

        RemComp<HeldByOniComponent>(args.Entity);

        if (TryComp<GunComponent>(args.Entity, out var gun))
        {
            _gun.RefreshModifiers((args.Entity,gun));
        }
    }

    private void OnOniMeleeHit(EntityUid uid, OniComponent component, MeleeHitEvent args)
    {
        args.ModifiersList.Add(component.MeleeModifiers);
    }

    private void OnHeldMeleeHit(EntityUid uid, HeldByOniComponent component, MeleeHitEvent args)
    {
        if (!TryComp<OniComponent>(component.Holder, out var oni))
            return;

        args.ModifiersList.Add(oni.MeleeModifiers);
    }

    private void OnStamHit(EntityUid uid, HeldByOniComponent component, StaminaMeleeHitEvent args)
    {
        if (!TryComp<OniComponent>(component.Holder, out var oni))
            return;

        args.Multiplier *= oni.StamDamageMultiplier;
    }
}
