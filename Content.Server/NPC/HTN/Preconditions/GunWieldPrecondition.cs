using Content.Server.Wieldable;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Wieldable.Components;

namespace Content.Server.NPC.HTN.Preconditions;

/// <summary>
/// Checks if the NPC's gun can be wielded when <see cref="GunRequiresWieldComponent"/> is present.
/// </summary>
public sealed partial class GunWieldPrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;

    [DataField("invert")]
    public bool Invert;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var gunSystem = _entManager.System<GunSystem>();
        var wieldSystem = _entManager.System<WieldableSystem>();

        if (!gunSystem.TryGetGun(owner, out var gunUid, out _))
            return Invert;

        if (!_entManager.HasComponent<GunRequiresWieldComponent>(gunUid))
            return !Invert;

        if (!_entManager.TryGetComponent(gunUid, out WieldableComponent? wieldable))
            return Invert;

        if (wieldable.Wielded)
            return !Invert;

        var canWield = wieldSystem.CanWield(gunUid, wieldable, owner, quiet: true);

        return Invert ? !canWield : canWield;
    }
}
