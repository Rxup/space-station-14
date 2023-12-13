using Content.Server.Backmen.NPC.Events;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Server.Backmen.NPC.Prototypes;

[Prototype("npcConversationTree")]
public sealed partial class NPCConversationTreePrototype : IPrototype, ISerializationHooks
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; } = default!;

    /// <summary>
    /// Dialogue contains all the topics to which an NPC can discuss.
    /// </summary>
    [ViewVariables]
    [DataField("dialogue", required: true)]
    public NPCTopic[] Dialogue { get; private set; } = default!;

    /// <summary>
    /// Attention responses are what the NPC says when they start paying
    /// attention to you without a specific question or prompt to respond to.
    /// </summary>
    [ViewVariables]
    [DataField("attention", required: true)]
    public NPCResponse[] Attention { get; private set; } = default!;

    /// <summary>
    /// Idle responses are just things the NPC will say when nothing else is
    /// going on, after some time.
    /// </summary>
    [ViewVariables]
    [DataField("idle", required: true)]
    public NPCResponse[] Idle { get; private set; } = default!;

    /// <summary>
    /// Unknown responses are what the NPC says when they can't respond to a
    /// particular question or prompt.
    /// </summary>
    [ViewVariables]
    [DataField("unknown", required: true)]
    public NPCResponse[] Unknown { get; private set; } = default!;

    /// <summary>
    /// Custom responses are available to use in extensions to the NPC
    /// Conversation system.
    /// </summary>
    // NOTE: This may be removed in favor of storing NPCResponses on custom
    // components, i.e. an NPCShopkeeperComponent, but for now, it lives here
    // to help demonstrate some features.
    [ViewVariables]
    [DataField("custom")]
    public Dictionary<string, NPCResponse[]> Custom { get; private set; } = new ();

    /// <summary>
    /// This exists as a quick way to map a prompt to a topic.
    /// </summary>
    public Dictionary<string, NPCTopic> PromptToTopic { get; private set; } = new();

    // ISerializationHooks _is_ obsolete, but ConstructionGraphPrototype is using it as of this commit,
    // and I'm not quite sure how to otherwise do this.
    //
    // I will look at that prototype when ISerializationHooks is phased out.
    void ISerializationHooks.AfterDeserialization()
    {
        // Cache the strings mapping to prompts.
        foreach (var topic in Dialogue)
        {
            foreach (var prompt in topic.Prompts)
            {
                PromptToTopic[prompt] = topic;
            }
        }
    }
}

[DataDefinition]
public sealed partial class NPCTopic
{
    [DataField("prompts")]
    public string[] Prompts { get; private set; } = default!;

    /// <summary>
    /// This determines the likelihood of this topic being selected over any
    /// other, given the existence of multiple candidates.
    /// </summary>
    [DataField("weight")]
    public float Weight { get; private set; } = 1.0f;

    /// <summary>
    /// Locked topics will not be accessible through dialogue until unlocked.
    /// </summary>
    [DataField("locked")]
    public bool Locked { get; private set; }

    /// <summary>
    /// Hidden topics won't show up in any form of "help" question.
    /// </summary>
    [DataField("hidden")]
    public bool Hidden { get; private set; }

    [DataField("responses", required: true)]
    public NPCResponse[] Responses { get; private set; } = default!;
}

[DataDefinition]
public sealed partial class NPCResponse
{
    public NPCResponse() { }

    public NPCResponse(string? text, SoundSpecifier? audio = null, NPCConversationEvent? ev = null)
    {
        Text = text;
        Audio = audio;
        Event = ev;
    }

    public override string ToString()
    {
        return $"NPCResponse({Text})";
    }

    [DataField("text")]
    public string? Text { get; private set; }

    [DataField("audio")]
    public SoundSpecifier? Audio { get; private set; }

    /* [DataField("emote")] */
    /* public string? Emote; */

    /// <summary>
    /// This event is raised when the response is queued,
    /// for the purpose of dynamic responses.
    /// </summary>
    [DataField("is")]
    public NPCConversationEvent? Is { get; private set; }

    /// <summary>
    /// This event is raised after the response is made.
    /// </summary>
    [DataField("event")]
    public NPCConversationEvent? Event { get; private set; }

    /// <summary>
    /// This event is raised when the NPC next hears a response,
    /// allowing the response to be processed by other systems.
    /// </summary>
    [DataField("listenEvent")]
    public NPCConversationListenEvent? ListenEvent { get; private set; }
}

