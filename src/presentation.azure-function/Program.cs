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
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
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
            setup.DefaultModelId = Models.ChatGpt3_5Turbo;
        });
        s.Configure<CosmosDbOptions>(opt =>
        {
            opt.DatabaseId = Environment.GetEnvironmentVariable("CosmosDatabase", EnvironmentVariableTarget.Process)
                ?? throw new ArgumentException("CosmosDatabase is missing");
            opt.BriefMessagesContainerId = Environment.GetEnvironmentVariable("CosmosTelegramMessagesContainer", EnvironmentVariableTarget.Process)
                ?? throw new ArgumentException("CosmosTelegramMessagesContainer is missing");
            opt.ConnectionString = Environment.GetEnvironmentVariable("CosmosDbConnectionString", EnvironmentVariableTarget.Process)
                ?? throw new ArgumentException("CosmosDbConnectionString is missing");
        });
        s.AddAzureClients(c =>
        {
            c.AddServiceBusClient(Environment.GetEnvironmentVariable("ServiceBusConnection", EnvironmentVariableTarget.Process));
        });
        s.AddSingleton(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<CosmosDbOptions>>().Value;
            return new CosmosClient(
                opt.ConnectionString,
                clientOptions: new CosmosClientOptions { MaxRetryAttemptsOnRateLimitedRequests = 3 });
        });
        s.AddScoped<TelegramMessageRepository>();
        s.AddScoped<ITokenCounter, TokenCounter>();
        s.AddScoped<IWhitelist, Whitelist>();
        s.AddScoped<TooLongDidnotReadToday>();
        s.AddScoped<UpdateHandler>();
    })
    .Build();

host.Run();
