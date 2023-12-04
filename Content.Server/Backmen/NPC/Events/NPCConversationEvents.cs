using Content.Server.Backmen.NPC.Systems;
using Robust.Shared.Audio;

namespace Content.Server.Backmen.NPC.Events;

/// <summary>
/// This is used for dynamic responses and post-response events.
/// </summary>
[ImplicitDataDefinitionForInheritors]
[Access(typeof(NPCConversationSystem))]
public abstract partial class NPCConversationEvent : EntityEventArgs
{
    /// <summary>
    /// This is the entity that the NPC is speaking to.
    /// </summary>
    public EntityUid? TalkingTo;
}

/// <summary>
/// This event type is raised when an NPC hears a response when it was set to listen for one.
/// </summary>
/// <remarks>
/// Set Handled to true when you want the NPC to stop listening.
/// The NPC will otherwise keep listening and block any attempt to find a prompt in the speaker's words.
/// </remarks>
[ImplicitDataDefinitionForInheritors]
[Access(typeof(NPCConversationSystem))]
public abstract partial class NPCConversationListenEvent : HandledEntityEventArgs
{
    /// <summary>
    /// This is the entity that said the message.
    /// </summary>
    public EntityUid? Speaker;

    /// <summary>
    /// This is the original message that the NPC heard.
    /// </summary>
    public string Message = default!;

    /// <summary>
    /// This is the message, parsed into separate words.
    /// </summary>
    public List<string> Words = default!;
}

public sealed partial class NPCConversationHelpEvent : NPCConversationEvent
{
    [DataField("text")]
    public string? Text { get; private set; }

    [DataField("audio")]
    public SoundSpecifier? Audio { get; private set; }
}

/// <summary>
/// This event can be raised after a response to cause an NPC to stop paying attention to someone.
/// </summary>
public sealed partial class NPCConversationByeEvent : NPCConversationEvent { }

// The following classes help demonstrate some of the features of the system.
// They may be separated out at some point.
public sealed partial class NPCConversationToldNameEvent : NPCConversationListenEvent { }

