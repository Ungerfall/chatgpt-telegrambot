namespace Ungerfall.ChatGpt.TelegramBot.Database;
public class CosmosDbOptions
{
    public string ConnectionString { get; set; } = null!;
    public string DatabaseId { get; set; } = null!;
    public string MessagesContainerId { get; set; } = null!;
    public string TimedTasksContainerId { get; set; } = null!;
}
