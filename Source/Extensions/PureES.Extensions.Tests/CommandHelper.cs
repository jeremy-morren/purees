using System.Diagnostics;
using System.Text;

namespace PureES.Extensions.Tests.EventStore;

public static class CommandHelper
{
    /// <summary>
    /// Runs a command and returns contents of <c>stdout</c> if successful
    /// </summary>
    /// <param name="command">Command to run</param>
    /// <param name="arguments">Argument list</param>
    /// <returns></returns>
    public static string RunCommand(string command, params string[] arguments)
    {
        var process = new Process()
        {
            StartInfo =
            {
                FileName = command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        foreach (var a in arguments)
            process.StartInfo.ArgumentList.Add(a);
        var @out = new StringBuilder();
        var err = new StringBuilder();
        process.OutputDataReceived += (_, e) => @out.AppendLine(e.Data);
        process.ErrorDataReceived += (_, e) => err.AppendLine(e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new Exception($"Command {command} returned exit code {process.ExitCode}",
                new Exception(err.ToString()));
        return @out.ToString();
    }
}