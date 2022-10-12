using CommandLine;

namespace PureES.Build.Verbs;

[Verb("app-version", HelpText = "Gets the current PureES version")]
public class GetVersion
{
    public static Task Invoke()
    {
        //No write-line, otherwise MSBuild will get a newline
        Console.Write(Build.Version);
        return Task.CompletedTask;
    }
}