using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace azure_function
{
    public class TelegramBot
    {
        private readonly ILogger _logger;

        public TelegramBot(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<TelegramBot>();
        }

        [Function("TelegramBot")]
        public HttpResponseData Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var tgToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN", EnvironmentVariableTarget.Process)
                ?? throw new ArgumentException("TELEGRAM_BOT_TOKEN is missing");
            var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.Process)
                ?? throw new ArgumentException("OPENAI_API_KEY is missing");
            var openAiOrg = Environment.GetEnvironmentVariable("OPENAI_ORG", EnvironmentVariableTarget.Process)
                ?? throw new ArgumentException("OPENAI_ORG is missing");


            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            response.WriteString("Welcome to Azure Functions!");

            return response;
        }
    }
}
