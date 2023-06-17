using SharpToken;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;

namespace Ungerfall.ChatGpt.TelegramBot;
public sealed class TokenCounter : ITokenCounter
{
    private static readonly GptEncoding _encoding = GptEncoding.GetEncodingForModel(OpenAI.GPT3.ObjectModels.Models.ChatGpt3_5Turbo);

    public int Count(string text)
    {
        return _encoding.Encode(text).Count;
    }
}
