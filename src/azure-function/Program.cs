using azure_function;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI.GPT3.Extensions;
using OpenAI.GPT3.ObjectModels;
using System;
using Telegram.Bot;

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
        s.AddScoped<UpdateService>();
    })
    .Build();

host.Run();
