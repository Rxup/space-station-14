using Content.Shared.Backmen.Surgery.Tools;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Backmen.Surgery.Traumas.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body.Organ;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedBodySystem), typeof(TraumaSystem))]
public sealed partial class OrganComponent : Component, ISurgeryToolComponent
{
    /// <summary>
    /// Relevant body this organ is attached to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Body;

    /// <summary>
    ///     Relevant body this organ originally belonged to.
    ///     ///     FOR WHATEVER FUCKING REASON AUTONETWORKING THIS CRASHES GIBTEST AAAAAAAAAAAAAAA
    /// </summary>
    [DataField]
    public EntityUid? OriginalBody;

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
    ///     Current Organ severity, dynamically updated based on organ integrity
    /// </summary>
    [DataField, AutoNetworkedField]
    public OrganSeverity OrganSeverity = OrganSeverity.Normal;

    /// <summary>
    ///     All the modifiers that are currently modifying the OrganIntegrity
    /// </summary>
    public Dictionary<(string, EntityUid), FixedPoint2> IntegrityModifiers = new();

    /// <summary>
    ///     The name's self-explanatory, thresholds. for states. of integrity. of this god fucking damn organ.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<OrganSeverity, FixedPoint2> IntegrityThresholds = new();

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
}
