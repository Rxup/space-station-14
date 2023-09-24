using Robust.Shared.Prototypes;
using Content.Server.Backmen.Abilities.Psionics;
using Content.Server.Backmen.NPC.Events;
using Content.Server.Backmen.NPC.Prototypes;
using Content.Server.Backmen.NPC.Systems;
using Content.Server.Backmen.StationEvents.Events;
using Content.Server.Radio.Components;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Backmen.Psionics.Glimmer;
using Content.Shared.Radio;

namespace Content.Server.Backmen.Research.SophicScribe;

public sealed partial class SophicScribeSystem : EntitySystem
{
    [Dependency] private readonly GlimmerSystem _glimmerSystem = default!;
    [Dependency] private readonly RadioSystem _radioSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly NPCConversationSystem _conversationSystem = default!;

    private readonly ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SophicScribeComponent, NPCConversationGetGlimmerEvent>(OnGetGlimmer);
        SubscribeLocalEvent<GlimmerEventEndedEvent>(OnGlimmerEventEnded);
    }

    private void OnGetGlimmer(EntityUid uid, SophicScribeComponent component, NPCConversationGetGlimmerEvent args)
    {
        if (args.Text == null)
        {
            _sawmill.Error($"{ToPrettyString(uid)} heard a glimmer reading prompt but has no text for it.");
            return;
        }

        var tier = _glimmerSystem.GetGlimmerTier() switch
        {
            GlimmerTier.Minimal => Loc.GetString("glimmer-reading-minimal"),
            GlimmerTier.Low => Loc.GetString("glimmer-reading-low"),
            GlimmerTier.Moderate => Loc.GetString("glimmer-reading-moderate"),
            GlimmerTier.High => Loc.GetString("glimmer-reading-high"),
            GlimmerTier.Dangerous => Loc.GetString("glimmer-reading-dangerous"),
            _ => Loc.GetString("glimmer-reading-critical"),
        };

        var glimmerReadingText = Loc.GetString(args.Text,
            ("glimmer", _glimmerSystem.Glimmer), ("tier", tier));

        var response = new NPCResponse(glimmerReadingText);
        _conversationSystem.QueueResponse(uid, response);
    }

    private void OnGlimmerEventEnded(GlimmerEventEndedEvent args)
    {
        var query = EntityQueryEnumerator<SophicScribeComponent>();
        while (query.MoveNext(out var scribe, out _))
        {
            if (!TryComp<IntrinsicRadioTransmitterComponent>(scribe, out var radio)) return;

            // mind entities when...
            var speaker = scribe;
            if (TryComp<MindSwappedComponent>(scribe, out var swapped))
            {
                speaker = swapped.OriginalEntity;
            }

            var message = Loc.GetString(args.Message, ("decrease", args.GlimmerBurned), ("level", _glimmerSystem.Glimmer));
            var channel = _prototypeManager.Index<RadioChannelPrototype>("Common");
            _radioSystem.SendRadioMessage(speaker, message, channel, speaker);
        }
    }
}

public sealed partial class NPCConversationGetGlimmerEvent : NPCConversationEvent
{
    [DataField("text")]
    public string? Text { get; private set; }
}
