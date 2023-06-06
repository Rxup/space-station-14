using Content.Shared.Objectives;
using Robust.Shared.Serialization;

namespace Content.Shared.CharacterInfo;

[Serializable, NetSerializable]
public sealed class RequestCharacterInfoEvent : EntityEventArgs
{
    public readonly EntityUid EntityUid;

    public RequestCharacterInfoEvent(EntityUid entityUid)
    {
        EntityUid = entityUid;
    }
}

[Serializable, NetSerializable]
public sealed class CharacterInfoEvent : EntityEventArgs
{
    public readonly EntityUid EntityUid;
    public readonly string JobTitle;
    public readonly Dictionary<string, List<ConditionInfo>> Objectives;
    public readonly string Briefing;

    // start-backmen: currency
    public readonly Dictionary<string, string> Memory = new();
    // end-backmen: currency

    public CharacterInfoEvent(EntityUid entityUid, string jobTitle, Dictionary<string, List<ConditionInfo>> objectives, string briefing)
    {
        EntityUid = entityUid;
        JobTitle = jobTitle;
        Objectives = objectives;
        Briefing = briefing;
    }

    // start-backmen: currency
    public CharacterInfoEvent(EntityUid entityUid, string jobTitle, Dictionary<string, List<ConditionInfo>> objectives, string briefing, Dictionary<string, string> memory)
    {
        EntityUid = entityUid;
        JobTitle = jobTitle;
        Objectives = objectives;
        Briefing = briefing;
        Memory = memory;
    }
    // end-backmen: currency
}
