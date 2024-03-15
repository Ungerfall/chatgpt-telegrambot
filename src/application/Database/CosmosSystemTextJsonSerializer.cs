#nullable disable
using Microsoft.Azure.Cosmos;
using System.IO;
using System.Text.Json;
using Ungerfall.ChatGpt.TelegramBot.SourceGenerators;

namespace Ungerfall.ChatGpt.TelegramBot;
/// <summary>
/// See https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/Microsoft.Azure.Cosmos.Samples/Usage/SystemTextJson/CosmosSystemTextJsonSerializer.cs
/// </summary>
public class CosmosSystemTextJsonSerializer : CosmosSerializer
{
    public override T FromStream<T>(Stream stream)
    {
        using (stream)
        {
            if (stream.CanSeek
                   && stream.Length == 0)
            {
                return default;
            }

            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                return (T)(object)stream;
            }

            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            return (T)JsonSerializer.Deserialize(memoryStream.ToArray(), typeof(T), CosmosContext.Default);
        }
    }

    public override Stream ToStream<T>(T input)
    {
        MemoryStream streamPayload = new();
        var buffer = JsonSerializer.SerializeToUtf8Bytes(input, input.GetType(), CosmosContext.Default);
        streamPayload.Write(buffer, 0, buffer.Length);
        streamPayload.Position = 0;
        return streamPayload;
    }
}