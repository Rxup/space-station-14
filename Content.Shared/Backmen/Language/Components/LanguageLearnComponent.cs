using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Backmen.Language.Components;

[RegisterComponent]
public sealed partial class LanguageLearnComponent : Component
{
    /// <summary>
    /// The language to be learned when the item is used.
    /// </summary>
    [DataField(required: true, customTypeSerializer: typeof(PrototypeIdSerializer<LanguagePrototype>))]
    public string Language { get; set; } = default!;

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
    /// Maximum number of times the item can be used.  If 0, the item is single-use.
    /// </summary>
    [DataField]
    public int MaxUses = 1;

    /// <summary>
    /// Whether the item should be deleted after the last use.
    /// </summary>
    [DataField]
    public bool DeleteAfterUse = false;

    [ViewVariables]
    public int? UsesRemaining = null;

    public int GetUsesRemaining()
    {
        return UsesRemaining ?? MaxUses;
    }
}
