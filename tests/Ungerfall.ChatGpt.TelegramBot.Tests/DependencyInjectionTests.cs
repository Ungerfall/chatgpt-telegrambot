using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Ungerfall.ChatGpt.TelegramBot.Tests;
public class DependencyInjectionTest
{
    public DependencyInjectionTest()
    {
        Environment.SetEnvironmentVariable("TELEGRAM_BOT_TOKEN", Guid.NewGuid().ToString(), EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", Guid.NewGuid().ToString(), EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("OPENAI_ORG", Guid.NewGuid().ToString(), EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("CosmosDatabase", Guid.NewGuid().ToString(), EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("CosmosTelegramMessagesContainer", Guid.NewGuid().ToString(), EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("CosmosTimedTasksContainer", Guid.NewGuid().ToString(), EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(
            "CosmosDbConnectionString",
            "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;",
            EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(
            "ServiceBusConnection",
            "Endpoint=sb://not-real.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=somekey",
            EnvironmentVariableTarget.Process);
    }

    [Fact]
    public void AllDependenciesAreRegistered()
    {
        // Arrange
        var host = AzureFunction.AzureFunctionHost.HostBuilder.Value;
        var serviceProvider = host.Services;

        // Act & Assert
        var assemblies = new[]
        {
            typeof(UpdateHandler).Assembly,
            typeof(AzureFunction.AzureFunctionHost).Assembly,
        };
        foreach (var type in assemblies.SelectMany(x => x.GetTypes()).Where(type => type.IsClass))
        {
            // Skip anonymous types
            if (type.GetCustomAttributes(typeof(CompilerGeneratedAttribute), inherit: false).Length != 0)
            {
                continue;
            }

            // Skip weird names
            if (type.Name.StartsWith("<>"))
            {
                continue;
            }

            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            foreach (var ctorType in constructors
                .Select(x => x.GetParameters())
                .Where(x => x.Length > 0)
                .SelectMany(x => x, (_, pi) => pi.ParameterType)
                .Where(x => assemblies.Contains(x.Assembly)))
            {
                try
                {
                    serviceProvider.GetRequiredService(ctorType);
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Failed to resolve dependencies of {ctorType.FullName} for {type.FullName}. Error: {ex.Message}");
                }
            }
        }
    }
}
