using Robust.Shared.Serialization;
namespace Content.Shared.Cards;
[RegisterComponent]
public sealed partial class FlipCardComponent : Component
{
    [ViewVariables, DataField("flipped")] public bool Flipped;
}

[Serializable, NetSerializable]
public enum CardsVisual
{
    Visual,
    Normal,
    Flipped
}