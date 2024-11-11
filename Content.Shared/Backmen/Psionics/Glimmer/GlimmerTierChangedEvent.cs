using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Psionics.Glimmer;

/// <summary>
/// This event is fired when the broader glimmer tier has changed,
/// not on every single adjustment to the glimmer count.
///
/// <see cref="GlimmerSystem.GetGlimmerTier"/> has the exact
/// values corresponding to tiers.
/// </summary>
[Serializable, NetSerializable]
public sealed class GlimmerTierChangedEvent : EntityEventArgs
{
    /// <summary>
    /// What was the last glimmer tier before this event fired?
    /// </summary>
    public readonly GlimmerTier LastTier;

    /// <summary>
    /// What is the current glimmer tier?
    /// </summary>
    public readonly GlimmerTier CurrentTier;

    /// <summary>
    /// What is the change in tiers between the last and current tier?
    /// </summary>
    public readonly int TierDelta;

    public GlimmerTierChangedEvent(GlimmerTier lastTier, GlimmerTier currentTier, int tierDelta)
    {
        LastTier = lastTier;
        CurrentTier = currentTier;
        TierDelta = tierDelta;
    }
}
