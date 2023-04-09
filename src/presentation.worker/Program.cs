using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenAI.GPT3.Extensions;
using OpenAI.GPT3.ObjectModels;
using System;
using Telegram.Bot;
using Ungerfall.ChatGpt.TelegramBot;
using Ungerfall.ChatGpt.TelegramBot.Database;
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
            opt.BriefMessagesContainerId = context.Configuration["CosmosTelegramMessagesContainer"] ?? throw new ArgumentNullException(nameof(context));
            opt.ConnectionString = context.Configuration["CosmosDbConnectionString"] ?? throw new ArgumentNullException(nameof(context));
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
            setup.DefaultModelId = Models.ChatGpt3_5Turbo;
        });
        services.AddAzureClients(c =>
        {
            c.AddClient<CosmosClient, CosmosDbOptions>(opt => new CosmosClient(
                opt.ConnectionString,
                clientOptions: new CosmosClientOptions { MaxRetryAttemptsOnRateLimitedRequests = 3 }));
            c.AddServiceBusClient(context.Configuration["ServiceBusConnection"]);
        });

        services.AddScoped<BriefTelegramMessageRepository>();
        services.AddScoped<PollingUpdateHandler>();
        services.AddScoped<ReceiverService>();
        services.AddScoped<UpdateHandler>();
        services.AddHostedService<PollingService>();
    })
    .Build();

await host.RunAsync();