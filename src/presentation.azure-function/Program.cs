using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenAI.Extensions;
using OpenAI.ObjectModels;
using System;
using Telegram.Bot;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.AzureFunction;
using Ungerfall.ChatGpt.TelegramBot.Commands;
using Ungerfall.ChatGpt.TelegramBot.Configuration;
using Ungerfall.ChatGpt.TelegramBot.Database;
using Ungerfall.ChatGpt.TelegramBot.TimedTasks;

var host = AzureFunctionHost.HostBuilder.Value;
host.Run();

namespace Ungerfall.ChatGpt.TelegramBot.AzureFunction
{
    public static class AzureFunctionHost
    {
        public static Lazy<IHost> HostBuilder { get; } = new(CreateHost, isThreadSafe: true);

        private static IHost CreateHost()
        {
            var tgToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN", EnvironmentVariableTarget.Process)
                ?? throw new ArgumentException("TELEGRAM_BOT_TOKEN is missing");
            var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.Process)
                ?? throw new ArgumentException("OPENAI_API_KEY is missing");
            var openAiOrg = Environment.GetEnvironmentVariable("OPENAI_ORG", EnvironmentVariableTarget.Process)
                ?? throw new ArgumentException("OPENAI_ORG is missing");

            return new HostBuilder()
               .ConfigureFunctionsWorkerDefaults()
               .ConfigureServices(s =>
               {
                   s.AddHttpClient("tgclient")
                       .AddTypedClient<ITelegramBotClient>(httpClient => new TelegramBotClient(tgToken, httpClient));
                   s.AddOpenAIService(setup =>
                   {
                       setup.ApiKey = openAiApiKey;
                       setup.Organization = openAiOrg;
                       setup.DefaultModelId = Models.Gpt_3_5_Turbo;
                   });
                   s.Configure<CosmosDbOptions>(opt =>
                   {
                       opt.DatabaseId = Environment.GetEnvironmentVariable(
                           "CosmosDatabase",
                           EnvironmentVariableTarget.Process)
                           ?? throw new ArgumentException("CosmosDatabase is missing");
                       opt.MessagesContainerId = Environment.GetEnvironmentVariable(
                           "CosmosTelegramMessagesContainer",
                           EnvironmentVariableTarget.Process)
                           ?? throw new ArgumentException("CosmosTelegramMessagesContainer is missing");
                       opt.ConnectionString = Environment.GetEnvironmentVariable(
                           "CosmosDbConnectionString",
                           EnvironmentVariableTarget.Process)
                           ?? throw new ArgumentException("CosmosDbConnectionString is missing");
                       opt.TimedTasksContainerId = Environment.GetEnvironmentVariable(
                           "CosmosTimedTasksContainer",
                           EnvironmentVariableTarget.Process)
                           ?? throw new ArgumentException("CosmosTimedTasksContainer is missing");
                   });
                   s.AddAzureClients(c => c.AddServiceBusClient(Environment.GetEnvironmentVariable(
                       "ServiceBusConnection",
                       EnvironmentVariableTarget.Process)));
                   s.AddSingleton(sp =>
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
                   s.AddScoped<ITelegramMessageRepository, TelegramMessageRepository>();
                   s.AddScoped<ITimedTaskExecutionRepository, TimedTaskExecutionRepository>();
                   s.AddScoped<ITokenCounter, TokenCounter>();
                   s.AddScoped<IWhitelist, Whitelist>();
                   s.AddScoped<TooLongDidNotReadToday>();
                   s.AddScoped<DailySummary>();
                   s.AddScoped<DailyQuiz>();
                   s.AddScoped<DailyFilmsQuiz>();
                   s.AddScoped<DailyVideoGamesQuiz>();
                   s.AddScoped<GenerateImage>();
                   s.AddScoped<UpdateHandler>();
                   s.AddSingleton<TestUsers>();
                   s.AddSingleton<QuizChats>();
                   s.AddSingleton<DailySummaryChats>();
               })
               .Build();
        }
    }
}
