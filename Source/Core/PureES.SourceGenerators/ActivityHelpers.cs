using System.Text;
using PureES.SourceGenerators.Framework;

namespace PureES.SourceGenerators;

internal static class ActivityHelpers
{
    private const string ActivitySource = "PureES.PureESTracing.ActivitySource";

    public static void StartActivity(IndentedWriter writer,
        string activityName,
        string displayName,
        IEnumerable<(string Name, string? Value)> tags)
    {
        //Because this handler is called without a parent activity (i.e. not from a http request), we provide a parent activity here
        writer.WriteLine($"using (var activity = {ActivitySource}.StartActivity({activityName.ToStringLiteral()}))");
        writer.PushBrace();

        writer.WriteStatement("if (activity != null)", () =>
        {
            writer.WriteLine($"activity.DisplayName = {displayName.ToStringLiteral()};");

            writer.WriteStatement("if (activity.IsAllDataRequested)", () =>
            {
                foreach (var (key, value) in tags)
                    if (value != null)
                        writer.WriteLine($"activity?.SetTag({key.ToStringLiteral()}, {value});");
            });
        });
    }

    /// <summary>
    /// Sets activity status to success
    /// </summary>
    public static void SetActivitySuccess(IndentedWriter writer)
    {
        writer.WriteLine($"activity?.SetStatus({StatusCodeOk});");
    }

    /// <summary>
    /// Set activity status to error
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="exception">Exception variable</param>
    public static void SetActivityError(IndentedWriter writer, string exception)
    {
        writer.WriteStatement("if (activity != null)", () =>
        {
            writer.WriteLine($"activity.SetStatus({StatusCodeError}, {exception}.Message);");
            writer.WriteLine($"activity.SetTag(\"error.type\", {exception}.GetType().FullName);");
            writer.WriteLine($"activity.AddException({exception});");
        });
    }

    private const string StatusCodeOk = "global::System.Diagnostics.ActivityStatusCode.Ok";
    private const string StatusCodeError = "global::System.Diagnostics.ActivityStatusCode.Error";


    /// <summary>
    /// Gets a display name for a type (including generic arguments, without namespace)
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static string GetTypeDisplayName(IType type)
    {
        if (!type.IsGenericType)
            return type.Name;
        var args = type.GenericArguments.Select(GetTypeDisplayName);
        return $"{type.Name}<{string.Join(", ", args)}>";
    }
}