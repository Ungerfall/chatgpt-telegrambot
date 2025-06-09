using Microsoft.Extensions.Logging;
using OpenAI.Images;
using System.ClientModel;
using System.ClientModel.Primitives;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Ungerfall.ChatGpt.TelegramBot.AzureOpenAi;

namespace Ungerfall.ChatGpt.TelegramBot.Commands;
public class GenerateImage(
    AzureOpenAIClient azureOpenAIClient,
    ILogger<GenerateImage> logger,
    ITelegramBotClient botClient)
{
    readonly ImageClient _imageClient = azureOpenAIClient.GetImageClient(Deployments.DallE3);

    public async Task<Message> Execute(Message message, string msgWithoutCommand, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(msgWithoutCommand))
        {
            return await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "Write an image description after the command.",
                replyParameters: message.MessageId,
                cancellationToken: cancellation);
        }

        await botClient.SendChatAction(
            chatId: message.Chat.Id,
            ChatAction.UploadPhoto,
            cancellationToken: cancellation);
        ClientResult<GeneratedImage> result = await _imageClient.GenerateImageAsync(
            prompt: msgWithoutCommand,
            new ImageGenerationOptions
            {
                Quality = GeneratedImageQuality.Standard,
                Size = GeneratedImageSize.W1024xH1024,
                Style = GeneratedImageStyle.Vivid,
                EndUserId = message.From?.Username ?? "unknown",
            },
            cancellation);
        PipelineResponse rawResponse = result.GetRawResponse();
        if (!rawResponse.IsError)
        {
            return await botClient.SendPhoto(
                chatId: message.Chat.Id,
                photo: InputFile.FromUri(result.Value.ImageUri),
                replyParameters: message.MessageId,
                hasSpoiler: true,
                cancellationToken: cancellation);
        }
        else
        {
            logger.LogError("ChatGPT error: {@Error}", new { rawResponse.ReasonPhrase });
            return await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "ChatGPT request wasn't successful." + rawResponse.ReasonPhrase,
                replyParameters: message.MessageId,
                cancellationToken: cancellation);
        }
    }
}
