using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace azure_function;

public class TelegramBot
{
    private readonly ILogger _logger;
    private readonly UpdateService _updateService;

    public TelegramBot(ILoggerFactory loggerFactory, UpdateService updateService)
    {
        _logger = loggerFactory.CreateLogger<TelegramBot>();
        _updateService = updateService;
    }

    [Function("TelegramBot")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
        CancellationToken cancellation)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        try
        {
            var body = await req.ReadAsStringAsync() ?? throw new ArgumentNullException(nameof(req));
            var update = JsonConvert.DeserializeObject<Update>(body);
            if (update is null)
            {
                _logger.LogWarning("Unable to deserialize Update object.");
                return response;
            }

            await _updateService.Handle(update, cancellation);
        }
        catch (Exception e)
        {
            _logger.LogError("Exception: {exception}", e.Message);
        }

        return response;
    }
}
