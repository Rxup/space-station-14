using Content.Shared.Damage;
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
    #region Live Data

    [ViewVariables]
    public EntityUid? Observer = default!;

    [ViewVariables]
    public HashSet<EntityUid> BlobTiles = [];

    [ViewVariables]
    public List<EntityUid> Actions = [];

    [ViewVariables]
    public TimeSpan NextAction = TimeSpan.Zero;

    #endregion

    #region Balance

    [DataField]
    public FixedPoint2 CoreBlobTotalHealth = 400;

    [DataField]
    public float AttackRate = 0.8f;

    [DataField]
    public bool CanSplit = true;

    #endregion

    #region Damage Specifiers

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

    #endregion

    #region Blob Chems

    [ViewVariables]
    public readonly Dictionary<BlobChemType, Color> ChemСolors = new()
    {
        {BlobChemType.ReactiveSpines, Color.FromHex("#637b19")},
        {BlobChemType.BlazingOil, Color.FromHex("#937000")},
        {BlobChemType.RegenerativeMateria, Color.FromHex("#441e59")},
        {BlobChemType.ExplosiveLattice, Color.FromHex("#6e1900")},
        {BlobChemType.ElectromagneticWeb, Color.FromHex("#0d7777")},
    };

    [DataField]
    public BlobChemType DefaultChem = BlobChemType.ReactiveSpines;

    [DataField]
    public BlobChemType CurrentChem = BlobChemType.ReactiveSpines;

    #endregion

    #region Blob Costs

    [DataField]
    public int ResourceBlobsTotal;

    [DataField]
    public FixedPoint2 AttackCost = 4;

    [DataField]
    public Dictionary<BlobTileType, FixedPoint2> BlobTileCosts = new()
    {
        {BlobTileType.Resource, 60},
        {BlobTileType.Factory, 80},
        {BlobTileType.Node, 50},
        {BlobTileType.Reflective, 15},
        {BlobTileType.Strong, 15},
        {BlobTileType.Normal, 6},
        {BlobTileType.Storage, 50},
        {BlobTileType.Turret, 75},
    };

    [DataField]
    public FixedPoint2 BlobbernautCost = 60;

    [DataField]
    public FixedPoint2 SplitCoreCost = 400;

    [DataField]
    public FixedPoint2 SwapCoreCost = 200;

    [DataField]
    public FixedPoint2 SwapChemCost = 70;

    #endregion

    #region Blob Ranges

    [DataField]
    public float NodeRadiusLimit = 4f;

    [DataField]
    public float TilesRadiusLimit = 7f;

    #endregion

    #region Prototypes

    [DataField]
    public Dictionary<BlobTileType, ProtoId<EntityPrototype>> TilePrototypes = new()
    {
        {BlobTileType.Resource, "ResourceBlobTile"},
        {BlobTileType.Factory, "FactoryBlobTile"},
        {BlobTileType.Node, "NodeBlobTile"},
        {BlobTileType.Reflective, "ReflectiveBlobTile"},
        {BlobTileType.Strong, "StrongBlobTile"},
        {BlobTileType.Normal, "NormalBlobTile"},
        //{BlobTileType.Storage, ""},
        //{BlobTileType.Turret, ""},
        {BlobTileType.Core, "CoreBlobTileGhostRole"},
    };

    [DataField(required: true)]
    public List<ProtoId<EntityPrototype>> ActionPrototypes = [];

    [DataField]
    public string CoreBlobTile = "CoreBlobTileGhostRole";

    [DataField]
    public string BlobExplosive = "Blob";

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ObserverBlobPrototype = "MobObserverBlob";

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<AntagPrototype>))]
    public string AntagBlobPrototypeId = "Blob";

    #endregion

    #region Sounds

    [DataField]
    public SoundSpecifier GreetSoundNotification = new SoundPathSpecifier("/Audio/Effects/clang.ogg");

    [DataField]
    public SoundSpecifier AttackSound = new SoundPathSpecifier("/Audio/Animals/Blob/blobattack.ogg");

    #endregion
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
