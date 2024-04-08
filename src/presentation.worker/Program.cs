using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenAI.Extensions;
using OpenAI.ObjectModels;
using System;
using Telegram.Bot;
using Ungerfall.ChatGpt.TelegramBot;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.Commands;
using Ungerfall.ChatGpt.TelegramBot.Database;
using Ungerfall.ChatGpt.TelegramBot.TimedTasks;
using Ungerfall.ChatGpt.TelegramBot.Worker;
using Ungerfall.ChatGpt.TelegramBot.Worker.Services;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Register Bot configuration
        services.Configure<Configuration>(opt =>
        {
            opt.TelegramBotToken = context.Configuration["TELEGRAM_BOT_TOKEN"] ?? throw new ArgumentNullException(nameof(context));
            opt.OpenAiApiKey = context.Configuration["OPENAI_API_KEY"] ?? throw new ArgumentNullException(nameof(context));
            opt.OpenAiOrg = context.Configuration["OPENAI_ORG"] ?? throw new ArgumentNullException(nameof(context));
        });
        services.Configure<CosmosDbOptions>(opt =>
        {
            opt.DatabaseId = context.Configuration["CosmosDatabase"] ?? throw new ArgumentNullException(nameof(context));
            opt.MessagesContainerId = context.Configuration["CosmosTelegramMessagesContainer"] ?? throw new ArgumentNullException(nameof(context));
            opt.ConnectionString = context.Configuration["CosmosDbConnectionString"] ?? throw new ArgumentNullException(nameof(context));
            opt.TimedTasksContainerId = context.Configuration["CosmosTimedTasksContainer"] ?? throw new ArgumentNullException(nameof(context));
        });

        services.AddHttpClient("telegram_bot_client")
                .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
                {
                    Configuration? botConfig = sp.GetRequiredService<IOptions<Configuration>>().Value;
                    TelegramBotClientOptions options = new(botConfig.TelegramBotToken);
                    return new TelegramBotClient(options, httpClient);
                });

        services.AddOpenAIService(setup =>
        {
            setup.ApiKey = context.Configuration["OPENAI_API_KEY"] ?? throw new ArgumentNullException(nameof(context));
            setup.Organization = context.Configuration["OPENAI_ORG"] ?? throw new ArgumentNullException(nameof(context));
            setup.DefaultModelId = Models.Gpt_3_5_Turbo;
        });
        services.AddAzureClients(c => c.AddServiceBusClient(context.Configuration["ServiceBusConnection"]));
        services.AddSingleton(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<CosmosDbOptions>>().Value;
            return new CosmosClient(
                opt.ConnectionString,
                clientOptions: new CosmosClientOptions
                {
                    MaxRetryAttemptsOnRateLimitedRequests = 3,
                    Serializer = new CosmosSystemTextJsonSerializer(),
                });
        });
        services.AddScoped<ITelegramMessageRepository, TelegramMessageRepository>();
        services.AddScoped<PollingUpdateHandler>();
        services.AddScoped<ReceiverService>();
        services.AddScoped<ITokenCounter, TokenCounter>();
        services.AddScoped<IWhitelist, Whitelist>();
        services.AddScoped<TooLongDidNotReadToday>();
        services.AddScoped<DailyTooLongDidNotRead>();
        services.AddScoped<DailyQuiz>();
        services.AddScoped<GenerateImage>();
        services.AddScoped<UpdateHandler>();
        services.AddHostedService<PollingService>();
    })
    .Build();

await host.RunAsync();