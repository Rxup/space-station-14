using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery.Pain.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NerveSystemComponent : Component
{
    /// <summary>
    /// Pain.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 Pain = 0f;

    /// <summary>
    /// How much Pain can this nerve system hold.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 PainCap = 100f;

    // Don't change, OR I will break your knees, filled up upon initialization.
    public Dictionary<EntityUid, NerveComponent> Nerves = new();

    // Don't add manually!! Use built-in functions.
    public Dictionary<string, PainMultiplier> Multipliers = new();
    public Dictionary<EntityUid, PainModifier> Modifiers = new();

    [DataField("lastThreshold"), ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 LastPainThreshold = 0f;

    [DataField("reflexThresholds"), ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<PainThresholdTypes, FixedPoint2> PainThresholds = new()
    {
        { PainThresholdTypes.PainSound, 8 },
        { PainThresholdTypes.PainShock, 24 },
        { PainThresholdTypes.PainPassout, 45 },
        // usually appears after an explosion. or some ultra big damage output thing, you might survive, and most importantly, you will fall down in pain.
        // :troll:
    };
}
