using Content.Shared.Backmen.Surgery.Tools;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body;

/// <summary>
/// Marks an entity as being able to be inserted into an entity with <seealso cref="BodyComponent" />.
/// </summary>
/// <seealso cref="BodySystem" />
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BodySystem))]
public sealed partial class OrganComponent : Component, ISurgeryToolComponent
{
    /// <summary>
    /// The body entity containing this organ, if any
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Body;

    /// <summary>
    /// What kind of organ is this, if any
    /// </summary>
    [DataField]
    public ProtoId<OrganCategoryPrototype>? Category;

    #region Surgery

    // backmen edit start
    /// <summary>
    ///     Maximal organ integrity, do keep in mind that Organs are supposed to be VERY and VERY damage sensitive
    /// </summary>
    [DataField("intCap"), AutoNetworkedField]
    public FixedPoint2 IntegrityCap = 15;

    /// <summary>
    ///     Current organ HP, or integrity, whatever you prefer to say
    /// </summary>
    [DataField("integrity"), AutoNetworkedField]
    public FixedPoint2 OrganIntegrity = 15;

    /// <summary>
    ///     Current Organ severity, dynamically updates based on organ integrity
    /// </summary>
    [DataField, AutoNetworkedField]
    public OrganSeverity OrganSeverity = OrganSeverity.Normal;

    /// <summary>
    ///     Sound played when this organ gets turned into a blood mush.
    /// </summary>
    [DataField]
    public SoundSpecifier OrganDestroyedSound = new SoundCollectionSpecifier("OrganDestroyed");

    /// <summary>
    ///     All the modifiers that are currently modifying the OrganIntegrity
    /// </summary>
    public Dictionary<(string, EntityUid), FixedPoint2> IntegrityModifiers = new();

    /// <summary>
    ///     The name's self-explanatory, thresholds. for states. of integrity. of this god fucking damn organ.
    /// </summary>
    [DataField]
    // TODO: Not "required" for now, and can break some shit BECAUSE I AM NOT reworking the entirety of ShitSurgerySystem to work properly without breaking the linter
    public Dictionary<OrganSeverity, FixedPoint2> IntegrityThresholds = new();

    /// <summary>
    ///     Rate at which the organ regenerates integrity over time (per frame).
    ///     Positive values heal the organ by reducing damage modifiers.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 OrganRegenerationRate = 0;
    // backmen edit end

    /// <summary>
    /// Shitcodey solution to not being able to know what name corresponds to each organ's slot ID
    /// without referencing the prototype or hardcoding.
    /// </summary>
    [DataField] //AlwaysPushInheritance
    public string SlotId = "";

    [DataField, AlwaysPushInheritance]
    public string ToolName { get; set; } = "An organ";

    [DataField, AlwaysPushInheritance]
    public float Speed { get; set; } = 1f;

    /// <summary>
    ///  If true, the organ will not heal an entity when transplanted into them.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool? Used { get; set; }

    /// <summary>
    ///     When attached, the organ will ensure these components on the entity, and delete them on removal.
    /// </summary>
    [DataField]
    public ComponentRegistry? OnAdd;

    /// <summary>
    ///     When removed, the organ will ensure these components on the entity, and add them on removal.
    /// </summary>
    [DataField]
    public ComponentRegistry? OnRemove;

    /// <summary>
    ///     Is this organ working or not?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    /// <summary>
    ///     Can this organ be enabled or disabled? Used mostly for prop, damaged or useless organs.
    /// </summary>
    [DataField]
    public bool CanEnable = true;

    #endregion
}
