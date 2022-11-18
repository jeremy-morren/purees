using System.IO.Pipelines;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using ContentDispositionHeaderValue = Microsoft.Net.Http.Headers.ContentDispositionHeaderValue;

namespace EventStoreBackup;

public class BackupService
{
    private readonly ILogger<BackupService> _logger;
    private readonly BackupOptions _options;

    public BackupService(ILogger<BackupService> logger, IOptions<BackupOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task Process(HttpResponse response,
        CompressionType? compression, 
        CancellationToken ct)
    {
        var date = DateTime.UtcNow;
        
        var baseDir = new DirectoryInfo(Path.Combine(_options.TempDirectory, Guid.NewGuid().ToString()));
        baseDir.Create();
        
        try
        {
            _logger.LogInformation("Creating backup in folder {BaseFolder}", baseDir.FullName);

            //tar will contain folder 'backup'
            var backupDir = baseDir.CreateSubdirectory("backup");

            WriteMetadata(baseDir, date);

            response.StatusCode = (int) HttpStatusCode.OK;
            SetHeaders(response.Headers, date, compression);
            
            //Return the headers and get going
            await response.StartAsync(ct);

            //Open the response stream
            var writer = response.BodyWriter;

            await PerformBackup(baseDir, backupDir, ct);
            
            //Write the TAR to the response body
            await CreateTar(baseDir, writer, compression, ct);
        }
        finally
        {
            baseDir.Delete(true);
        }
    }
    
    #region Setup

    private void WriteMetadata(DirectoryInfo baseDir, DateTime date)
    {
        var metadata = baseDir.CreateSubdirectory("metadata");

        //Just an informational files in the directory
        File.Create(Path.Combine(metadata.FullName, $"{date:yyyy-MM-dd}_{date:HH:mm:ss}")); //NB: This filename could cause problems on Windows
        File.Create(Path.Combine(metadata.FullName, _options.ServerName));

        File.WriteAllText(Path.Combine(metadata.FullName, "metadata.json"),
            JsonSerializer.Serialize(new
            {
                date,
                server = _options.ServerName
            }),
            new UTF8Encoding(false));
    }

    private void SetHeaders(IHeaderDictionary headers, DateTime date, CompressionType? compression)
    {
        var extension = compression switch
        {
            CompressionType.Gzip => "tar.gz",
            CompressionType.Bzip2 => "tar.bz2",
            _ => "tar"
        };
        headers[HeaderNames.ContentDisposition] = new ContentDispositionHeaderValue("attachment")
        {
            FileName = $"{_options.ServerName}_{date:yyyy-MM-dd}_{date:HH:mm:ss}.{extension}",
            CreationDate = new DateTimeOffset(date)
        }.ToString();
        headers[HeaderNames.ContentType] = compression switch
        {
            CompressionType.Gzip => "application/tar+gzip",
            CompressionType.Bzip2 => "application/tar+bzip2",
            _ => "application/tar"
        };
    }
    
    #endregion
    
    #region Backup

    //Actually do the backup, see https://developers.eventstore.com/server/v20.10/operations.html#simple-full-backup-restore
    private async Task PerformBackup(DirectoryInfo baseDir, FileSystemInfo backupDir, CancellationToken ct)
    {
        //We will run through /bin/sh to use globbing
        
        var dataDir = new DirectoryInfo(_options.DataDirectory);
        
        async Task Rsync(string flags, IEnumerable<string> args)
        {
            var argList = new List<string>() {flags};
            argList.AddRange(args);
            argList.Add(backupDir.FullName); //All commands end with backup dir
            
            await ExecHelper.RunCommand(dataDir, "rsync", argList, ct);
        }

        var index = Path.Combine(dataDir.FullName, "index");

        await Rsync("-aIR" ,GlobFiles(index, "*.chk", true).Select(f => $"index/{f}"));
        await Rsync("-aI", new [] { "--exclude", "*.chk", "index" });
        await Rsync("-aI", GlobFiles(dataDir, "*.chk", false));
        await Rsync("-a", GlobFiles(dataDir, "chunk-*.*", false));
    }

    private static IEnumerable<string> GlobFiles(DirectoryInfo baseDir, string pattern, bool recursive) =>
        baseDir.GetFiles(pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Select(f => f.FullName[(baseDir.FullName.Length + 1)..]);

    private static IEnumerable<string> GlobFiles(string baseDir, string pattern, bool recursive) =>
        GlobFiles(new DirectoryInfo(baseDir), pattern, recursive);

    private static async Task CreateTar(DirectoryInfo baseDir,
        PipeWriter output, 
        CompressionType? compressionType,
        CancellationToken ct)
    {
        //Make files readonly
        await ExecHelper.RunCommand(baseDir, "chmod", new [] { "-R", "644", baseDir.FullName }, ct);
        
        //Directories require Execute to list
        var directories = baseDir.GetDirectories("*", SearchOption.AllDirectories)
            .Select(d => d.FullName);
        await ExecHelper.RunCommand(baseDir, "chmod", new[] {"755"}.Concat(directories), ct);
        
        var comp = compressionType switch
        {
            CompressionType.Gzip => "z",
            CompressionType.Bzip2 => "j",
            _ => null
        };
        await ExecHelper.RunCommand(baseDir,
            "tar",
            new[] {"c", $"-O{comp}", "backup/", "metadata/"},
            output,
            ct);
    }
    
    #endregion
}