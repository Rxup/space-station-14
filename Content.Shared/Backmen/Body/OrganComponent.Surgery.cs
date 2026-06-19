using Content.Shared.Backmen.Surgery.Tools;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Backmen.Surgery.Traumas.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body;

public sealed partial class OrganComponent
{
    [DataField]
    public EntityUid? BodyPart;

    [DataField]
    public EntityUid? OriginalBody;

    [DataField("intCap"), AutoNetworkedField]
    public FixedPoint2 IntegrityCap = 15;

    [DataField("integrity"), AutoNetworkedField]
    public FixedPoint2 OrganIntegrity = 15;

    [DataField, AutoNetworkedField]
    public OrganSeverity OrganSeverity = OrganSeverity.Normal;

    [DataField]
    public SoundSpecifier OrganDestroyedSound = new SoundCollectionSpecifier("OrganDestroyed");

    public Dictionary<(string, EntityUid), FixedPoint2> IntegrityModifiers = new();

    [DataField]
    public Dictionary<OrganSeverity, FixedPoint2> IntegrityThresholds = new();

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 OrganRegenerationRate = 0;

    [DataField, AlwaysPushInheritance]
    public string SlotId = "";

    [DataField, AlwaysPushInheritance]
    public string ToolName { get; set; } = "An organ";

    [DataField, AlwaysPushInheritance]
    public float Speed { get; set; } = 1f;

    [DataField, AutoNetworkedField]
    public bool? Used { get; set; }

    [DataField]
    public ComponentRegistry? OnAdd;

    [DataField]
    public ComponentRegistry? OnRemove;

    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField]
    public bool CanEnable = true;
}
