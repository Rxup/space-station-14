using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Surgery.Pain.Systems;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared.Backmen.Surgery.Traumas.Systems;

public abstract partial class TraumaSystem : EntitySystem
{
    [Dependency] protected readonly IRobustRandom Random = default!;

    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    [Dependency] protected readonly SharedContainerSystem Container = default!;
    [Dependency] protected readonly SharedBodySystem Body = default!;

    [Dependency] protected readonly WoundSystem Wound = default!;
    [Dependency] protected readonly PainSystem Pain = default!;
    [Dependency] protected readonly ConsciousnessSystem Consciousness = default!;

    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;

    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtual = default!;

    [Dependency] private readonly SharedAudioSystem _audio = default!;

    protected EntityQuery<OrganComponent> OrganQuery;
    protected EntityQuery<BoneComponent> BoneQuery;

    private float _organTraumaSlowdownTimeMultiplier;
    private float _organTraumaWalkSpeedSlowdown;
    private float _organTraumaRunSpeedSlowdown;

    private readonly Dictionary<WoundableSeverity, FixedPoint2> _boneTraumaChanceMultipliers = new()
    {
        { WoundableSeverity.Healthy, 0 },
        { WoundableSeverity.Minor, 0.01 },
        { WoundableSeverity.Moderate, 0.04 },
        { WoundableSeverity.Severe, 0.12 },
        { WoundableSeverity.Critical, 0.21 },
        { WoundableSeverity.Loss, 0.21 },
    };

    public override void Initialize()
    {
        base.Initialize();

        InitProcess();

        InitBones();
        InitOrgans();

        OrganQuery = GetEntityQuery<OrganComponent>();
        BoneQuery = GetEntityQuery<BoneComponent>();

        Subs.CVar(_cfg, CCVars.OrganTraumaSlowdownTimeMultiplier, value => _organTraumaSlowdownTimeMultiplier = value, true);
        Subs.CVar(_cfg, CCVars.OrganTraumaWalkSpeedSlowdown, value => _organTraumaWalkSpeedSlowdown = value, true);
        Subs.CVar(_cfg, CCVars.OrganTraumaRunSpeedSlowdown, value => _organTraumaRunSpeedSlowdown = value, true);
    }
}
