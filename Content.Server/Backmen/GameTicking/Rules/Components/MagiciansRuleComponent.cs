using Content.Server.GameTicking.Rules;
using Robust.Shared.Audio;

namespace Content.Server.Backmen.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(MagiciansRuleSystem))]
public sealed partial class MagiciansRuleComponent : Component
{
    [ViewVariables]
    public List<EntityUid> Magicians = new();
    [ViewVariables]
    public EntityUid MagicianShip = EntityUid.Invalid;

    [ViewVariables]
    public int MaxMagicians = 4;

    [ViewVariables]
    public int MinPlayers = 45;

    [ViewVariables]
    public int MagPerPlayer = 10;
    /// <summary>
    ///     Path to antagonist alert sound.
    /// </summary>
    [DataField("magiciansAlertSound")]
    public SoundSpecifier MagsAlertSound = new SoundPathSpecifier(
        "/Audio/Backmen/Antags/ragesmages.ogg",
        AudioParams.Default.WithVolume(4));
}
