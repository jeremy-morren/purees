namespace EventStoreBackup;

public class BackupOptions
{
    public string DataDirectory { get; set; } = null!;

    public string TempDirectory { get; set; } = "/tmp";

    public string ServerName { get; set; } = Environment.MachineName;

    public void Validate()
    {
        if (string.IsNullOrEmpty(DataDirectory))
            throw new InvalidOperationException("Data directory is required");
        if (!Directory.Exists(DataDirectory))
            throw new InvalidOperationException($"Directory {DataDirectory} does not exist");

        if (string.IsNullOrEmpty(TempDirectory))
            throw new InvalidOperationException("Temp directory is required");
        if (!Directory.Exists(TempDirectory))
            throw new InvalidOperationException($"Directory {TempDirectory} does not exist");

        if (string.IsNullOrEmpty(ServerName))
            throw new InvalidOperationException("Server name is required");
    }
}