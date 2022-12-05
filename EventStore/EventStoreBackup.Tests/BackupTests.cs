using System.IO.Compression;
using Xunit.Abstractions;

namespace EventStoreBackup.Tests;

public class BackupTests
{
    private readonly ITestOutputHelper _output;

    public BackupTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task CreateBackup()
    {
        await using var app = new WebApp(_output);
        var filename = Path.GetTempFileName();
        try
        {
            var client = app.CreateClient();
            var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("admin/backup?compression=gzip", UriKind.Relative),
                Headers = {Host = "a.esdb.local"}
            };
            using (var response = await client.SendAsync(request))
            {
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync();
                await using var fs = new FileStream(filename, FileMode.Create, FileAccess.Write);
                await stream.CopyToAsync(fs);
            }
            var info = new FileInfo(filename);
            Assert.True(info.Exists);
            Assert.NotEqual(0, info.Length);
            await using (var fs = info.OpenRead())
            {
                //Ensure that it is a valid gzip stream
                await using var gz = new GZipStream(fs, CompressionMode.Decompress, false);
                
                var buffer = new byte[1024];
                var _ = await gz.ReadAsync(buffer);
            }
        }
        finally
        {
            if (File.Exists(filename))
                File.Delete(filename);
        }
    }
}