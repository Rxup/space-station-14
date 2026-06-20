using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Surgery.Pain.Systems;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Backmen.Body.Systems;
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
    [Dependency] protected IRobustRandom Random = default!;

    [Dependency] private INetManager _net = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    [Dependency] protected SharedContainerSystem Container = default!;
    [Dependency] protected BkmBodySharedSystem Body = default!;

    [Dependency] protected WoundSystem Wound = default!;
    [Dependency] protected PainSystem Pain = default!;
    [Dependency] protected ConsciousnessSystem Consciousness = default!;

    [Dependency] protected MobStateSystem MobState = default!;

    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    [Dependency] private InventorySystem _inventory = default!;

    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedVirtualItemSystem _virtual = default!;

    [Dependency] private SharedAudioSystem _audio = default!;

    protected EntityQuery<WoundableComponent> WoundableQuery;
    protected EntityQuery<BodyPartComponent> BodyPartQuery;
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

        WoundableQuery = GetEntityQuery<WoundableComponent>();
        BodyPartQuery = GetEntityQuery<BodyPartComponent>();
        OrganQuery = GetEntityQuery<OrganComponent>();
        BoneQuery = GetEntityQuery<BoneComponent>();

        Subs.CVar(_cfg, CCVars.OrganTraumaSlowdownTimeMultiplier, value => _organTraumaSlowdownTimeMultiplier = value, true);
        Subs.CVar(_cfg, CCVars.OrganTraumaWalkSpeedSlowdown, value => _organTraumaWalkSpeedSlowdown = value, true);
        Subs.CVar(_cfg, CCVars.OrganTraumaRunSpeedSlowdown, value => _organTraumaRunSpeedSlowdown = value, true);
    }
}
