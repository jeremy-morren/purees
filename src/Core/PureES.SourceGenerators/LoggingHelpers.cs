using PureES.SourceGenerators.Framework;

namespace PureES.SourceGenerators;

internal static class LoggingHelpers
{

    public static void BeginLogScope(IndentedWriter writer, IEnumerable<(string, string?)> tags)
    {
        writer.WriteLine($"using (_logger.BeginScope(new {LoggerScopeType}()");
        writer.Push();
        writer.PushBrace();
        foreach (var (key, value) in tags)
            writer.WriteLine($"{{ {key.ToStringLiteral()}, {value ?? "null"} }},");
        writer.Pop();
        writer.WriteLine("}))");
        writer.Pop();
        writer.PushBrace();
    }

    private static readonly string LoggerScopeType =
        TypeNameHelpers.GetGenericTypeName(typeof(Dictionary<string, object>), "string", "object");
}