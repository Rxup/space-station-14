using Content.Shared.Body;
using Content.Shared.Inventory;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Backmen.Body.Systems;

public abstract partial class BkmBodySharedSystem : EntitySystem
{
    /*
     * See the body partial for how this works.
     */

    /// <summary>
    /// Container ID prefix for any body parts.
    /// </summary>
    public const string PartSlotContainerIdPrefix = "body_part_slot_";

    /// <summary>
    /// Container ID for the ContainerSlot on the body entity itself.
    /// </summary>
    public const string BodyRootContainerId = "body_root_part";

    /// <summary>
    /// Container ID prefix for anybody organs.
    /// </summary>
    public const string OrganSlotContainerIdPrefix = "body_organ_slot_";

    [Dependency] private IRobustRandom _random = default!; // backmen edit
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private InventorySystem _inventorySystem = default!; // backmen edit
    [Dependency] protected IPrototypeManager Prototypes = default!;
    [Dependency] protected INetManager Net = default!;
    [Dependency] protected MovementSpeedModifierSystem Movement = default!;
    [Dependency] protected SharedContainerSystem Containers = default!;
    [Dependency] protected SharedTransformSystem SharedTransform = default!;
    [Dependency] protected StandingStateSystem Standing = default!;
    [Dependency] private BodySystem _nubody = default!;
    [Dependency] protected DamageableSystem Damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitializeBody();
        InitializeParts();
        InitializeOrgans();
        // To try and mitigate the server load due to integrity checks, we set up a Job Queue.
        InitializePartAppearances();
    }

    /// <summary>
    /// Inverse of <see cref="GetPartSlotContainerId"/>
    /// </summary>
    public static string GetPartSlotContainerIdFromContainer(string containerSlotId)
    {
        // This is blursed
        var slotIndex = containerSlotId.IndexOf(PartSlotContainerIdPrefix, StringComparison.Ordinal);

        if (slotIndex < 0)
            slotIndex = 0;

        var slotId = containerSlotId.Remove(slotIndex, PartSlotContainerIdPrefix.Length);
        return slotId;
    }

    /// <summary>
    /// Gets the container ID for the specified slotId.
    /// </summary>
    public static string GetPartSlotContainerId(string slotId)
    {
        return PartSlotContainerIdPrefix + slotId;
    }

    /// <summary>
    /// Gets the container ID for the specified slotId.
    /// </summary>
    public static string GetOrganContainerId(string slotId)
    {
        return OrganSlotContainerIdPrefix + slotId;
    }
}
