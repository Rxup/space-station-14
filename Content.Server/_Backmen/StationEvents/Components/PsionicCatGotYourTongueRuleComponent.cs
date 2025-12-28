using Content.Server._Backmen.StationEvents.Events;
using Robust.Shared.Audio;

namespace Content.Server._Backmen.StationEvents.Components;

[RegisterComponent, Access(typeof(PsionicCatGotYourTongueRule))]
public sealed partial class PsionicCatGotYourTongueRuleComponent : Component
{
    [DataField("minDuration")]
    public TimeSpan MinDuration = TimeSpan.FromSeconds(20);

    [DataField("maxDuration")]
    public TimeSpan MaxDuration = TimeSpan.FromSeconds(80);

    [DataField("sound")]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_Nyanotrasen/Voice/Felinid/cat_scream1.ogg");
}
