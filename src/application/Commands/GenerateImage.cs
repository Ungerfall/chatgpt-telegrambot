﻿using Microsoft.Extensions.Logging;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Ungerfall.ChatGpt.TelegramBot.Commands;
public class GenerateImage
{
    private readonly IOpenAIService _openAiService;
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<GenerateImage> _logger;

    public GenerateImage(
        IOpenAIService openAiService,
        ILogger<GenerateImage> logger,
        ITelegramBotClient botClient)
    {
        _openAiService = openAiService;
        _logger = logger;
        _botClient = botClient;
    }

    public async Task<Message> Execute(Message message, string msgWithoutCommand, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(msgWithoutCommand))
        {
            return await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Write an image description after the command.",
                replyToMessageId: message.MessageId,
                cancellationToken: cancellation);
        }

        await _botClient.SendChatActionAsync(
            chatId: message.Chat.Id,
            ChatAction.UploadPhoto,
            cancellationToken: cancellation);
        var image = await _openAiService.Image.CreateImage(
            new OpenAI.ObjectModels.RequestModels.ImageCreateRequest
            {
                Prompt = msgWithoutCommand,
                Size = StaticValues.ImageStatics.Size.Size256,
                N = 1,
                User = message.From?.Username ?? "unknown",
            },
            cancellation);
        if (image.Successful)
        {
            return await _botClient.SendPhotoAsync(
                chatId: message.Chat.Id,
                photo: InputFile.FromUri(image.Results[0].Url),
                hasSpoiler: true,
                replyToMessageId: message.MessageId,
                cancellationToken: cancellation);
        }
        else
        {
            _logger.LogError("ChatGPT error: {@error}", new { image.Error?.Type, image.Error?.Message });
            return await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "ChatGPT request wasn't successful." + (image.Error?.Message ?? string.Empty),
                replyToMessageId: message.MessageId,
                cancellationToken: cancellation);
        }
    }
}
