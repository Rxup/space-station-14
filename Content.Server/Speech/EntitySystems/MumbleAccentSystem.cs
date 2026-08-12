using Content.Server.Chat.Systems;
using Content.Server.Speech.Components;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Server.Speech.EntitySystems;

// start-backmen: relay-accents
public sealed partial class MumbleAccentSystem : RelayAccentSystem<MumbleAccentComponent>
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private ReplacementAccentSystem _replacement = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MumbleAccentComponent, EmoteEvent>(OnEmote, before: [typeof(VocalSystem)]);
    }

    private void OnEmote(Entity<MumbleAccentComponent> ent, ref EmoteEvent args)
    {
        if (args.Handled || !args.Emote.Category.HasFlag(EmoteCategory.Vocal))
            return;

        if (TryComp<VocalComponent>(ent.Owner, out var vocalComp) && vocalComp.EmoteSounds is { } sounds)
        {
            // play a muffled version of the vocal emote
            args.Handled = _chat.TryPlayEmoteSound(
                ent.Owner,
                _prototype.Index(sounds),
                args.Emote,
                ent.Comp.EmoteAudioParams);
        }
    }

    public string Accentuate(string message, MumbleAccentComponent component)
    {
        return _replacement.ApplyReplacements(message, "mumble");
    }

    protected override string AccentuateInternal(EntityUid uid, MumbleAccentComponent comp, string message)
    {
        return Accentuate(message, comp);
    }
}
// end-backmen: relay-accents
