using Azure;
using Azure.AI.OpenAI;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using Telegram.Bot;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.AzureFunction;
using Ungerfall.ChatGpt.TelegramBot.Commands;
using Ungerfall.ChatGpt.TelegramBot.Configuration;
using Ungerfall.ChatGpt.TelegramBot.Database;
using Ungerfall.ChatGpt.TelegramBot.TimedTasks;

var host = AzureFunctionHost.HostBuilder.Value;
await host.RunAsync();

namespace Ungerfall.ChatGpt.TelegramBot.AzureFunction
{
    public static class AzureFunctionHost
    {
        public static Lazy<IHost> HostBuilder { get; } = new(CreateHost, isThreadSafe: true);

        private static IHost CreateHost()
        {
            var tgToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN", EnvironmentVariableTarget.Process)
                ?? throw new ArgumentException("TELEGRAM_BOT_TOKEN is missing");

            return new HostBuilder()
               .ConfigureFunctionsWorkerDefaults()
               .ConfigureServices(s =>
               {
                   s.AddHttpClient("tgclient")
                       .AddTypedClient<ITelegramBotClient>(httpClient => new TelegramBotClient(tgToken, httpClient));
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
                   s.AddAzureClients(c =>
                   {
                       c.AddServiceBusClient(Environment.GetEnvironmentVariable("ServiceBusConnection", EnvironmentVariableTarget.Process));
                       c.AddClient<AzureOpenAIClient, AzureOpenAIClientOptions>(opt =>
                           new AzureOpenAIClient(
                               new Uri(Environment.GetEnvironmentVariable("AZURE_OPENAI_URI", EnvironmentVariableTarget.Process) ?? throw new ArgumentNullException(nameof(opt))),
                               new AzureKeyCredential(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY", EnvironmentVariableTarget.Process) ?? throw new ArgumentNullException(nameof(opt))),
                               opt));
                   });
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
