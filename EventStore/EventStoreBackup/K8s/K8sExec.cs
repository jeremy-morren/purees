using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using k8s;
using k8s.Models;
// ReSharper disable InconsistentNaming

namespace EventStoreBackup.K8s;

public class K8sExec
{
    private readonly Kubernetes _client;
    private readonly ILogger<K8sExec> _logger;

    public K8sExec(Kubernetes client,
        ILogger<K8sExec> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<K8sExecResponse> Exec(V1Pod pod, IEnumerable<string> command, CancellationToken ct)
    {
        var cmd = command.ToList();
        try
        {
            _logger.LogInformation("Executing command {Command} on pod {@Pod}", FormatCommand(cmd), pod);
        
            var websocket = await _client.WebSocketNamespacedPodExecAsync(name: pod.Name(), 
                @namespace: pod.Namespace(), 
                command: cmd,
                container: pod.Spec.Containers[0].Name, //Always use the first container
                stdin: false,
                stdout: true,
                stderr: true,
                tty: false,
                cancellationToken: ct);
        
            var demux = new StreamDemuxer(websocket);
            demux.Start();

            await using var error = demux.GetStream(ChannelIndex.Error, ChannelIndex.Error);
            await using var stdErr = demux.GetStream(ChannelIndex.StdErr, ChannelIndex.StdErr);
            await using var stdOut = demux.GetStream(ChannelIndex.StdOut, ChannelIndex.StdOut);

            var readStdErrTask = ReadTerminal(stdErr, ct);
            var readStdOutTask = ReadTerminal(stdOut, ct);

            var response =
                await JsonSerializer.DeserializeAsync<K8sExecResponse>(error, K8sExecResponse.JsonOptions, ct) ??
                throw new InvalidOperationException("Error response is null");

            response.StdErr = await readStdErrTask;
            response.StdOut = await readStdOutTask;
            
            switch (response.Status)
            {
                case K8sExecStatus.Failure:

                    _logger.LogWarning("Error executing command {Command} in pod {@Pod}. {@Error}", 
                        FormatCommand(cmd), pod, response);
                    throw new K8sExecException(FormatCommand(cmd), response);
                default:
                    return response;
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Error executing command {Command} in pod {@Pod}", 
                FormatCommand(cmd), pod);
            throw;
        }
    }
    
    public async Task<K8sExecResponse> Exec(V1Pod pod, IEnumerable<string> command, PipeWriter pipeOut, CancellationToken ct)
    {
        var cmd = command.ToList();
        try
        {
            _logger.LogInformation("Executing command {Command} on pod {@Pod}", FormatCommand(cmd), pod);
        
            var websocket = await _client.WebSocketNamespacedPodExecAsync(name: pod.Name(), 
                @namespace: pod.Namespace(), 
                command: cmd,
                container: pod.Spec.Containers[0].Name, //Always use the first container
                stdin: false,
                stdout: true,
                stderr: true,
                tty: false,
                cancellationToken: ct);
        
            var demux = new StreamDemuxer(websocket);
            demux.Start();

            await using var error = demux.GetStream(ChannelIndex.Error, ChannelIndex.Error);
            await using var stdErr = demux.GetStream(ChannelIndex.StdErr, ChannelIndex.StdErr);
            await using var stdOut = demux.GetStream(ChannelIndex.StdOut, ChannelIndex.StdOut);

            var readStdErrTask = ReadTerminal(stdErr, ct);
            var readStdOutTask = stdOut.CopyToAsync(pipeOut, ct);

            var response =
                await JsonSerializer.DeserializeAsync<K8sExecResponse>(error, K8sExecResponse.JsonOptions, ct) ??
                throw new InvalidOperationException("Error response is null");

            response.StdErr = await readStdErrTask;
            await readStdOutTask;
            
            switch (response.Status)
            {
                case K8sExecStatus.Failure:

                    _logger.LogWarning("Error executing command {Command} in pod {@Pod}. {@Error}", 
                        FormatCommand(cmd), pod, response);
                    throw new K8sExecException(FormatCommand(cmd), response);
                default:
                    return response;
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Error executing command {Command} in pod {@Pod}", 
                FormatCommand(cmd), pod);
            throw;
        }
    }

    private static async Task<string?> ReadTerminal(Stream stream, CancellationToken ct)
    {
        var bytes = Array.Empty<byte>();
        var buffer = new byte[1024];
        int length;
        while ((length = await stream.ReadAsync(buffer, ct)) > 0)
        {
            Array.Resize(ref bytes, bytes.Length + length);
            Buffer.BlockCopy(buffer, 0, bytes, bytes.Length - length, length);
        }
        return bytes.Length > 0 ? Utf8Encoding.GetString(bytes) : null;
    }
    
    private static string FormatCommand(IEnumerable<string> cmd)
    {
        var list = cmd.Select(a =>
        {
            if (!a.Contains(' ') && !a.Contains('"'))
                return a;
            a = a.Replace("\"", "\\\""); // " -> \"
            return $"\"{a}\"";
        });
        return string.Join(" ", list);
    }

    private static readonly Encoding Utf8Encoding = new UTF8Encoding(false);
}