using System.Text.Json;

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

public record GptResponseApiChoiseFunctionCall(string name, JsonElement arguments)
{
    public T? DecodeArgs<T>() where T : class
    {
        if (arguments.ValueKind == JsonValueKind.Object)
        {
            return arguments.Deserialize<T>();
        }
        if (arguments.ValueKind == JsonValueKind.String)
        {
            return JsonSerializer.Deserialize<T>(arguments.GetString() ?? "{}");
        }

        return null;
    }
}
public record GptResponseApiChoiceMsg(string? content, string role, GptResponseApiChoiseFunctionCall? function_call, string? functions_state_id);
public record GptResponseApiChoice(int index, GptResponseApiChoiceMsg message, string finish_reason);
public record GptResponseApi(GptResponseApiChoice[] choices);
public record GigaTocResponse(string access_token, long expires_at);

#endregion
// ReSharper restore InconsistentNaming
