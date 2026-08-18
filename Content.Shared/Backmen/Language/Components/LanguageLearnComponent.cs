using Content.Shared.Backmen.Language;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Language.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class LanguageLearnComponent : Component
{
    /// <summary>
    /// The languages to be learned when the item is used.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<LanguagePrototype>> Languages = new();

    /// <summary>
    /// The amount of time it takes to learn the language.
    /// </summary>
    [DataField]
    public float DoAfterDuration = 3f;

    /// <summary>
    /// The sound to play when the item is used.
    /// </summary>
    [DataField]
    public SoundSpecifier? UseSound = new SoundPathSpecifier("/Audio/Items/Paper/paper_scribble1.ogg");

    /// <summary>
    /// Maximum number of times the item can be used.
    /// </summary>
    [DataField]
    public int MaxUses = 1;

    /// <summary>
    /// Whether the item should be deleted after the last use.
    /// </summary>
    [DataField]
    public bool DeleteAfterUse = false;

    /// <summary>
    /// Current number of uses remaining.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int? UsesRemaining = null;

    public int GetUsesRemaining()
    {
        return UsesRemaining ?? MaxUses;
    }
}
