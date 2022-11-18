using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Text;
using Serilog;

namespace EventStoreBackup;

public static class ExecHelper
{
    private static Serilog.ILogger Log => Serilog.Log.ForContext(typeof(ExecHelper));

    /// <summary>
    /// Runs a command and returns contents of <c>stdout</c> if successful
    /// </summary>
    /// <param name="workingDirectory">Working directory to start process in</param>
    /// <param name="command">Command to run</param>
    /// <param name="arguments">Argument list</param>
    /// <param name="ct" />
    /// <returns></returns>
    public static async Task<string> RunCommand(string workingDirectory, string command, IEnumerable<string> arguments, CancellationToken ct)
    {
        var args = arguments.ToList();
        var process = new Process()
        {
            StartInfo =
            {
                WorkingDirectory = workingDirectory,
                FileName = command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Environment =
                {
                    {"NO_COLOR", "1"}
                }
            }
        };
        foreach (var a in args)
            process.StartInfo.ArgumentList.Add(a);
        Log.Information("Executing command {Command}", FormatCommand(command, args));
        var @out = new StringBuilder();
        var err = new StringBuilder();
        process.OutputDataReceived += (_, e) => @out.AppendLine(e.Data);
        process.ErrorDataReceived += (_, e) => err.AppendLine(e.Data);
        ct.ThrowIfCancellationRequested();
        process.Start();
        ct.Register(() => process.Kill());
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(ct);
        ct.ThrowIfCancellationRequested();
        if (process.ExitCode == 0) 
            return @out.ToString();
        
        var e = new ExecException(FormatCommand(command, args), process.ExitCode, err.ToString());
        Log.Warning(e, "Error running command");
        throw e;
    }

    public static Task<string> RunCommand(DirectoryInfo workingDirectory, string command, IEnumerable<string> arguments, CancellationToken ct)
        => RunCommand(workingDirectory.FullName, command, arguments, ct);


    /// <summary>
    /// Runs a command and returns contents of <c>stdout</c> if successful
    /// </summary>
    /// <param name="workingDirectory">Working directory to start process in</param>
    /// <param name="command">Command to run</param>
    /// <param name="arguments">Argument list</param>
    /// <param name="destination">Destination to copy output</param>
    /// <param name="ct" />
    /// <returns></returns>
    public static async Task RunCommand(string workingDirectory, 
        string command, 
        IEnumerable<string> arguments, 
        PipeWriter destination,
        CancellationToken ct)
    {
        var args = arguments.ToList();
        var process = new Process()
        {
            StartInfo =
            {
                WorkingDirectory = workingDirectory,
                FileName = command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Environment =
                {
                    {"NO_COLOR", "1"}
                }
            }
        };
        foreach (var a in args)
            process.StartInfo.ArgumentList.Add(a);
        Log.Information("Executing command {Command}", FormatCommand(command, args));
        var err = new StringBuilder();
        process.ErrorDataReceived += (_, e) => err.AppendLine(e.Data);
        process.Start();
        ct.Register(() => process.Kill());
        process.BeginErrorReadLine();

        var copyTask = process.StandardOutput.BaseStream.CopyToAsync(destination, ct);
        
        await process.WaitForExitAsync(ct);
        ct.ThrowIfCancellationRequested();
        await copyTask;
        
        if (process.ExitCode == 0) 
            return;

        var e = new ExecException(FormatCommand(command, args), process.ExitCode, err.ToString());
        Log.Warning(e, "Error running command");
        throw e;
    }

    public static Task RunCommand(DirectoryInfo workingDirectory,
        string command,
        IEnumerable<string> arguments, 
        PipeWriter destination, 
        CancellationToken ct)
        => RunCommand(workingDirectory.FullName, command, arguments, destination, ct);

    private static string FormatCommand(string command, IEnumerable<string> args)
    {
        var list = args.Select(a =>
        {
            if (!a.Contains(' ') && !a.Contains('"'))
                return a;
            a = a.Replace("\"", "\\\""); // " -> \"
            return $"\"{a}\"";
        });
        return $"{command} {string.Join(" ", list)}";
    }
}

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class ExecException : Exception
{
    public string Command { get; }
    public int ExitCode { get; }
    public string OutError { get; }

    public ExecException(string command, int exitCode, string outError)
        : base($"'{command}' returned exit code {exitCode}", new Exception(outError))
    {
        Command = command;
        ExitCode = exitCode;
        OutError = outError;
    }
}