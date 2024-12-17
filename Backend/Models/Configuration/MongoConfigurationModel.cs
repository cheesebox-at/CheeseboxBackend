namespace Backend.Models.Configuration;

public class MongoConfigurationModel
{
    public required string ConnectionUri { get; init; }
    public required string DatabaseName { get; init; }
}