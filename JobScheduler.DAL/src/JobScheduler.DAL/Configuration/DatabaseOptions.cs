namespace JobScheduler.DAL.Configuration;

public class DatabaseOptions
{
    public string PostgresWriteConnectionString { get; set; } = string.Empty;
    public List<string> PostgresReadConnectionStrings { get; set; } = new();
}
