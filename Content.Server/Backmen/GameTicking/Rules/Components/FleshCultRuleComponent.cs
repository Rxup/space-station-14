using Content.Shared.Mind;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Backmen.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(FleshCultRuleSystem))]
public sealed partial class FleshCultRuleComponent : Component
{
    public SoundSpecifier AddedSound = new SoundPathSpecifier(
        "/Audio/Animals/Flesh/flesh_culstis_greeting.ogg");

    public SoundSpecifier BuySuccesSound = new SoundPathSpecifier(
        "/Audio/Animals/Flesh/flesh_cultist_buy_succes.ogg");

    public List<(EntityUid mindId, MindComponent mind)> Cultists = new();

    [DataField("fleshCultistPrototypeId")]
    public EntProtoId<MindRoleComponent> FleshCultistMindRolePrototypeId = "MindRoleFlesh";

    [DataField("fleshCultistLeaderPrototypeID")]
    public EntProtoId<MindRoleComponent> FleshCultistLeaderMindRolePrototypeId = "MindRoleFleshLeader";

    [DataField("faction")]
    public ProtoId<NpcFactionPrototype> Faction = default!;

    public int TotalCultists => Cultists.Count;

    public readonly List<string> CultistsNames = new();

    public WinTypes WinType = WinTypes.Fail;

    public EntityUid? TargetStation;

    public List<string> SpeciesWhitelist = new()
    {
        "Human",
        "Reptilian",
        "Dwarf",
        "Oni",
        "Vox",
        "HumanoidFoxes",
    };

    public enum WinTypes
    {
        FleshHeartFinal,
        Fail
    }

    public enum SelectionState
    {
        WaitingForSpawn = 0,
        ReadyToSelect = 1,
        SelectionMade = 2,
    }

    public SelectionState SelectionStatus = SelectionState.WaitingForSpawn;
    public Dictionary<ICommonSession, HumanoidCharacterProfile> StartCandidates = new();
}
