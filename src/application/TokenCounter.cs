using SharpToken;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;

namespace Ungerfall.ChatGpt.TelegramBot;
public sealed class TokenCounter : ITokenCounter
{
    private static readonly GptEncoding Encoding = GptEncoding.GetEncodingForModel(OpenAI.ObjectModels.Models.Gpt_4);

    public int Count(string text)
    {
        return Encoding.Encode(text).Count;
    }
}
