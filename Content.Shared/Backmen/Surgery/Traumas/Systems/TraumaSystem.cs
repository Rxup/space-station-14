using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Surgery.Pain.Systems;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared.Backmen.Surgery.Traumas.Systems;

[Virtual]
public sealed partial class TraumaSystem : EntitySystem
{
    [Dependency] private readonly PainSystem _pain = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly WoundSystem _wound = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ConsciousnessSystem _conciousness = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("traumas");

        InitBones();
    }
}
