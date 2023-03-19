using ChatGPT.TelegramBot.Services;
using ChatGPT.TelegramBot.Worker;
using Microsoft.Extensions.Options;
using OpenAI.GPT3.Extensions;
using OpenAI.GPT3.ObjectModels;
using Telegram.Bot;

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

        services.AddScoped<UpdateHandler>();
        services.AddScoped<ReceiverService>();
        services.AddHostedService<PollingService>();
    })
    .Build();

await host.RunAsync();