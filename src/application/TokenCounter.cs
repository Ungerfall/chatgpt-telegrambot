namespace Ungerfall.ChatGpt.TelegramBot;
public sealed class TokenCounter
{
    public int Count(string text)
    {
        var translitirated = Unidecode.NET.Unidecoder.Unidecode(text);
        return OpenAI.GPT3.Tokenizer.GPT3.TokenizerGpt3.TokenCount(translitirated);
    }
}
