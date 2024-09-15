using Content.Shared.Damage;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Backmen.Blob.Components;

[RegisterComponent,NetworkedComponent,AutoGenerateComponentState]
public sealed partial class BlobCoreComponent : Component
{
    [DataField("antagBlobPrototypeId")]
    public ProtoId<AntagPrototype> AntagBlobPrototypeId = "Blob";

    [ViewVariables(VVAccess.ReadWrite), DataField("attackRate"), AutoNetworkedField]
    public FixedPoint2 AttackRate = 0.8f;

    [ViewVariables(VVAccess.ReadWrite), DataField("returnResourceOnRemove"), AutoNetworkedField]
    public FixedPoint2 ReturnResourceOnRemove = 0.3f;

    [ViewVariables(VVAccess.ReadWrite), DataField("canSplit"), AutoNetworkedField]
    public bool CanSplit = true;

    [DataField("attackSound"), AutoNetworkedField]
    public SoundSpecifier AttackSound = new SoundPathSpecifier("/Audio/Animals/Blob/blobattack.ogg");

    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public Dictionary<BlobChemType, DamageSpecifier> ChemDamageDict { get; set; } = new()
    {
        {
            BlobChemType.BlazingOil, new DamageSpecifier()
            {
                DamageDict = new Dictionary<string, FixedPoint2>
                {
                    { "Heat", 15 },
                    { "Structural", 150 },
                }
            }
        },
        {
            BlobChemType.ReactiveSpines, new DamageSpecifier()
            {
                DamageDict = new Dictionary<string, FixedPoint2>
                {
                    { "Blunt", 8 },
                    { "Slash", 8 },
                    { "Piercing", 8 },
                    { "Structural", 150 },
                }
            }
        },
        {
            BlobChemType.ExplosiveLattice, new DamageSpecifier()
            {
                DamageDict = new Dictionary<string, FixedPoint2>
                {
                    { "Heat", 5 },
                    { "Structural", 150 },
                }
            }
        },
        {
            BlobChemType.ElectromagneticWeb, new DamageSpecifier()
            {
                DamageDict = new Dictionary<string, FixedPoint2>
                {
                    { "Structural", 150 },
                    { "Heat", 20 },
                },
            }
        },
        {
            BlobChemType.RegenerativeMateria, new DamageSpecifier()
            {
                DamageDict = new Dictionary<string, FixedPoint2>
                {
                    { "Structural", 150 },
                    { "Poison", 15 },
                }
            }
        },
    };

    [ViewVariables(VVAccess.ReadOnly)]
    public readonly Dictionary<BlobChemType, Color> ChemСolors = new()
    {
        {BlobChemType.ReactiveSpines, Color.FromHex("#637b19")},
        {BlobChemType.BlazingOil, Color.FromHex("#937000")},
        {BlobChemType.RegenerativeMateria, Color.FromHex("#441e59")},
        {BlobChemType.ExplosiveLattice, Color.FromHex("#6e1900")},
        {BlobChemType.ElectromagneticWeb, Color.FromHex("#0d7777")},
    };

    [ViewVariables(VVAccess.ReadOnly), DataField("blobExplosive"), AutoNetworkedField]
    public ProtoId<ExplosionPrototype> BlobExplosive = "Blob";

    [ViewVariables(VVAccess.ReadOnly), DataField("defaultChem"), AutoNetworkedField]
    public BlobChemType DefaultChem = BlobChemType.ReactiveSpines;

    [ViewVariables(VVAccess.ReadOnly), DataField("currentChem"), AutoNetworkedField]
    public BlobChemType CurrentChem = BlobChemType.ReactiveSpines;

    [ViewVariables(VVAccess.ReadWrite), DataField("factoryRadiusLimit"), AutoNetworkedField]
    public float FactoryRadiusLimit = 6f;

    [ViewVariables(VVAccess.ReadWrite), DataField("resourceRadiusLimit"), AutoNetworkedField]
    public float ResourceRadiusLimit = 3f;

    [ViewVariables(VVAccess.ReadWrite), DataField("nodeRadiusLimit"), AutoNetworkedField]
    public float NodeRadiusLimit = 4f;

    [ViewVariables(VVAccess.ReadWrite), DataField("attackCost"), AutoNetworkedField]
    public FixedPoint2 AttackCost = 2;

    [ViewVariables(VVAccess.ReadWrite), DataField("factoryBlobCost"), AutoNetworkedField]
    public FixedPoint2 FactoryBlobCost = 60;

    [ViewVariables(VVAccess.ReadWrite), DataField("normalBlobCost"), AutoNetworkedField]
    public FixedPoint2 NormalBlobCost = 4;

    [ViewVariables(VVAccess.ReadWrite), DataField("resourceBlobCost"), AutoNetworkedField]
    public FixedPoint2 ResourceBlobCost = 40;

    [ViewVariables(VVAccess.ReadWrite), DataField("nodeBlobCost"), AutoNetworkedField]
    public FixedPoint2 NodeBlobCost = 50;

    [ViewVariables(VVAccess.ReadWrite), DataField("blobbernautCost"), AutoNetworkedField]
    public FixedPoint2 BlobbernautCost = 60;

    [ViewVariables(VVAccess.ReadWrite), DataField("strongBlobCost"), AutoNetworkedField]
    public FixedPoint2 StrongBlobCost = 15;

    [ViewVariables(VVAccess.ReadWrite), DataField("reflectiveBlobCost"), AutoNetworkedField]
    public FixedPoint2 ReflectiveBlobCost = 15;

    [ViewVariables(VVAccess.ReadWrite), DataField("splitCoreCost"), AutoNetworkedField]
    public FixedPoint2 SplitCoreCost = 100;

    [ViewVariables(VVAccess.ReadWrite), DataField("swapCoreCost"), AutoNetworkedField]
    public FixedPoint2 SwapCoreCost = 80;

    [ViewVariables(VVAccess.ReadWrite), DataField("swapChemCost"), AutoNetworkedField]
    public FixedPoint2 SwapChemCost = 40;

    [ViewVariables(VVAccess.ReadWrite), DataField("reflectiveBlobTile"), AutoNetworkedField]
    public EntProtoId<BlobTileComponent> ReflectiveBlobTile = "ReflectiveBlobTile";

    [ViewVariables(VVAccess.ReadWrite), DataField("strongBlobTile"), AutoNetworkedField]
    public EntProtoId<BlobTileComponent> StrongBlobTile = "StrongBlobTile";

    [ViewVariables(VVAccess.ReadWrite), DataField("normalBlobTile"), AutoNetworkedField]
    public EntProtoId<BlobTileComponent> NormalBlobTile = "NormalBlobTile";

    [ViewVariables(VVAccess.ReadWrite), DataField("factoryBlobTile"), AutoNetworkedField]
    public EntProtoId FactoryBlobTile = "FactoryBlobTile";

    [ViewVariables(VVAccess.ReadWrite), DataField("resourceBlobTile"), AutoNetworkedField]
    public EntProtoId<BlobTileComponent> ResourceBlobTile = "ResourceBlobTile";

    [ViewVariables(VVAccess.ReadWrite), DataField("nodeBlobTile"), AutoNetworkedField]
    public string NodeBlobTile = "NodeBlobTile";

    [ViewVariables(VVAccess.ReadWrite), DataField("coreBlobTile"), AutoNetworkedField]
    public string CoreBlobTile = "CoreBlobTileGhostRole";

    [ViewVariables(VVAccess.ReadWrite), DataField("coreBlobTotalHealth"), AutoNetworkedField]
    public FixedPoint2 CoreBlobTotalHealth = 400;

    [ViewVariables(VVAccess.ReadWrite),
     DataField("ghostPrototype", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), AutoNetworkedField]
    public string ObserverBlobPrototype = "MobObserverBlob";

    [DataField("greetSoundNotification"), AutoNetworkedField]
    public SoundSpecifier GreetSoundNotification = new SoundPathSpecifier("/Audio/Effects/clang.ogg");

    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? Observer = default!;

    [ViewVariables(VVAccess.ReadOnly)]
    public HashSet<EntityUid> BlobTiles = new();

    [AutoNetworkedField]
    public TimeSpan NextAction = TimeSpan.Zero;

    [DataField("actionHelpBlob")]
    public EntityUid? ActionHelpBlob = null;
    [DataField("actionSwapBlobChem")]
    public EntityUid? ActionSwapBlobChem = null;
    [DataField("actionTeleportBlobToCore")]
    public EntityUid? ActionTeleportBlobToCore = null;
    [DataField("actionTeleportBlobToNode")]
    public EntityUid? ActionTeleportBlobToNode = null;
    [DataField("actionCreateBlobFactory")]
    public EntityUid? ActionCreateBlobFactory = null;
    [DataField("actionCreateBlobResource")]
    public EntityUid? ActionCreateBlobResource = null;
    [DataField("actionCreateBlobNode")]
    public EntityUid? ActionCreateBlobNode = null;
    [DataField("actionCreateBlobbernaut")]
    public EntityUid? ActionCreateBlobbernaut = null;
    [DataField("actionSplitBlobCore")]
    public EntityUid? ActionSplitBlobCore = null;
    [DataField("actionSwapBlobCore")]
    public EntityUid? ActionSwapBlobCore = null;
}

[Serializable, NetSerializable]
public enum BlobChemType : byte
{
    BlazingOil,
    ReactiveSpines,
    RegenerativeMateria,
    ExplosiveLattice,
    ElectromagneticWeb
}
