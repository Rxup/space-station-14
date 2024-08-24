using System.Text.Json;

namespace Content.Server.Backmen.Administration.Bwoink.Gpt.Models;

public sealed class GptMessageChat : GptMessage
{
    public string Message { get; set; }

    public GptMessageChat(GptUserDirection role, string message)
    {
        Role = role;
        Message = message;
    }

    public override object ToApi()
    {
        return new { role = Role.ToString(), content = Message };
    }
}

public sealed class GptMessageCallFunction : GptMessage
{
    private readonly GptResponseApiChoiceMsg _msg;

    public GptMessageCallFunction(GptResponseApiChoiceMsg msg)
    {
        _msg = msg;
    }

    public override object ToApi()
    {
        return new { role = GptUserDirection.assistant.ToString(), content = _msg.content ?? "", functions_state_id = _msg.functions_state_id, function_call = _msg.function_call };
    }
}

public sealed class GptMessageFunction : GptMessage
{
    public string Name { get; set; }
    public string Message { get; set; }

    public GptMessageFunction(string functionName, object functionResult)
    {
        Role = GptUserDirection.function;
        Name = functionName;
        Message = JsonSerializer.Serialize(functionResult);
    }

    public GptMessageFunction(string functionName)
    {
        Name = functionName;
        Message = "{}";
    }

    public override object ToApi()
    {
        return new { role = GptUserDirection.function.ToString(), content = Message, name = Name };
    }
}
