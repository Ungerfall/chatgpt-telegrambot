using ChatGPT.TelegramBot.Services;
using ChatGPT.TelegramBot.Worker;
using Microsoft.Extensions.Options;
using Telegram.Bot;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Register Bot configuration
        services.Configure<Configuration>(opt =>
        {
            opt.TelegramBotToken = context.Configuration["TELEGRAM_BOT_TOKEN"] ?? throw new ArgumentNullException(nameof(opt));
            opt.OpenAiApiKey = context.Configuration["OPENAI_API_KEY"] ?? throw new ArgumentNullException(nameof(opt));
            opt.OpenAiOrg = context.Configuration["OPENAI_ORG"] ?? throw new ArgumentNullException(nameof(opt));
        });

        services.AddHttpClient("telegram_bot_client")
                .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
                {
                    Configuration? botConfig = sp.GetRequiredService<IOptions<Configuration>>().Value;
                    TelegramBotClientOptions options = new(botConfig.TelegramBotToken);
                    return new TelegramBotClient(options, httpClient);
                });

        services.AddScoped<UpdateHandler>();
        services.AddScoped<ReceiverService>();
        services.AddHostedService<PollingService>();
    })
    .Build();

await host.RunAsync();