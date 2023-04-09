namespace Ungerfall.ChatGpt.TelegramBot.Database;
public class CosmosDbOptions
{
    public string ConnectionString { get; set; } = null!;
    public string DatabaseId { get; set; } = null!;
    public string BriefMessagesContainerId { get; set; } = null!;
}
