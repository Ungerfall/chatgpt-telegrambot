using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenAI.Extensions;
using OpenAI.ObjectModels;
using System;
using System.Text.Json.Serialization;
using Telegram.Bot;
using Ungerfall.ChatGpt.TelegramBot;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.AzureFunction;
using Ungerfall.ChatGpt.TelegramBot.Commands;
using Ungerfall.ChatGpt.TelegramBot.Database;

var tgToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN", EnvironmentVariableTarget.Process)
    ?? throw new ArgumentException("TELEGRAM_BOT_TOKEN is missing");
var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.Process)
    ?? throw new ArgumentException("OPENAI_API_KEY is missing");
var openAiOrg = Environment.GetEnvironmentVariable("OPENAI_ORG", EnvironmentVariableTarget.Process)
    ?? throw new ArgumentException("OPENAI_ORG is missing");

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(s =>
    {
        s.AddHttpClient("tgclient")
            .AddTypedClient<ITelegramBotClient>(httpClient => new TelegramBotClient(tgToken, httpClient));
        s.AddOpenAIService(setup =>
        {
            setup.ApiKey = openAiApiKey;
            setup.Organization = openAiOrg;
            setup.DefaultModelId = Models.Gpt_4;
        });
        s.Configure<CosmosDbOptions>(opt =>
        {
            opt.DatabaseId = Environment.GetEnvironmentVariable("CosmosDatabase", EnvironmentVariableTarget.Process)
                ?? throw new ArgumentException("CosmosDatabase is missing");
            opt.MessagesContainerId = Environment.GetEnvironmentVariable("CosmosTelegramMessagesContainer", EnvironmentVariableTarget.Process)
                ?? throw new ArgumentException("CosmosTelegramMessagesContainer is missing");
            opt.ConnectionString = Environment.GetEnvironmentVariable("CosmosDbConnectionString", EnvironmentVariableTarget.Process)
                ?? throw new ArgumentException("CosmosDbConnectionString is missing");
        });
        s.AddAzureClients(c => c.AddServiceBusClient(Environment.GetEnvironmentVariable("ServiceBusConnection", EnvironmentVariableTarget.Process)));
        s.AddSingleton(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<CosmosDbOptions>>().Value;
            return new CosmosClient(
                opt.ConnectionString,
                clientOptions: new CosmosClientOptions
                {
                    MaxRetryAttemptsOnRateLimitedRequests = 3,
                    Serializer = new CosmosSystemTextJsonSerializer(new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                        Converters =
                        {
                            new JsonStringEnumConverter()
                        },
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    })
                });
        });
        s.AddScoped<ITelegramMessageRepository, TelegramMessageRepository>();
        s.AddScoped<ITokenCounter, TokenCounter>();
        s.AddScoped<IWhitelist, Whitelist>();
        s.AddScoped<TooLongDidnotReadToday>();
        s.AddScoped<GenerateImage>();
        s.AddScoped<UpdateHandler>();
    })
    .Build();

host.Run();
