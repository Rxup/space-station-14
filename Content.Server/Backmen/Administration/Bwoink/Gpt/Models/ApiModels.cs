namespace Content.Server.Backmen.Administration.Bwoink.Gpt.Models;

// ReSharper disable InconsistentNaming
public enum GptUserDirection
{
    user,
    assistant,
    system,
    function
}

#region ParamApi

public record GptApiPacket(string model, object[] messages, List<object> functions, float temperature = 0.7f)
{
    public bool stream = false;
}

#endregion

#region ResponseApi

public record GptResponseApiChoiseFunctionCall(string name, string arguments);
public record GptResponseApiChoiceMsg(string? content, string role, GptResponseApiChoiseFunctionCall? function_call);
public record GptResponseApiChoice(int index, GptResponseApiChoiceMsg message, string finish_reason);
public record GptResponseApi(GptResponseApiChoice[] choices);

#endregion
// ReSharper restore InconsistentNaming
