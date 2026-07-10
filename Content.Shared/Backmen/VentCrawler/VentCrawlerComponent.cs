using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.ViewVariables;

namespace Content.Shared.Backmen.VentCrawler;

/// <summary>
/// Marks an entity as able to crawl through atmospheric vent pipe networks.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedVentCrawlerSystem))]
public sealed partial class VentCrawlerComponent : Component
{
    [DataField]
    public float EnterRange = 1.5f;

    [DataField]
    public float StepDelay = 0.35f;

    [DataField]
    public TimeSpan EnterCooldown = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan NextEnterAt;

    /// <summary>
    /// Whether pipe pressure affects this entity while vent crawling.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool PressureDamage = true;

    /// <summary>
    /// Whether pipe temperature affects this entity while vent crawling.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool TemperatureDamage = true;

    [DataField]
    public SoundSpecifier EnterSound = new SoundPathSpecifier(
        "/Audio/Ambience/Objects/gas_hiss.ogg",
        AudioParams.Default.WithVolume(-2f));

    [DataField]
    public SoundSpecifier ExitSound = new SoundPathSpecifier(
        "/Audio/Ambience/Objects/gas_hiss.ogg",
        AudioParams.Default.WithVolume(-2f));

    [DataField]
    public SoundSpecifier StepSound = new SoundCollectionSpecifier(
        "VentCrawlerStep",
        AudioParams.Default.WithVolume(-2f).WithVariation(0.15f));
}
