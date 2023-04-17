using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace Ungerfall.ChatGpt.TelegramBot.AzureFunction;

public class TelegramBot
{
    private readonly ILogger _logger;

    public TelegramBot(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<TelegramBot>();
    }

    [Function("TelegramBot")]
    public async Task<TelegramBotOutput> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        var output = new TelegramBotOutput
        {
            HttpResponseData = req.CreateResponse(HttpStatusCode.OK),
        };
        output.HttpResponseData.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        try
        {
            var body = await req.ReadAsStringAsync() ?? throw new ArgumentNullException(nameof(req));
            var update = JsonConvert.DeserializeObject<Update>(body);
            if (update is null)
            {
                _logger.LogWarning("Unable to deserialize Update object.");
                return output;
            }

            output.Update = update;
        }
        catch (Exception e)
        {
            _logger.LogError("Exception: {exception}", e.Message);
        }

        return output;
    }
}

public class TelegramBotOutput
{
    public HttpResponseData HttpResponseData { get; set; } = null!;

    [ServiceBusOutput(Const.TGBOT_UPDATES, Connection = "ServiceBusConnection")]
    public Update? Update { get; set; }
}
