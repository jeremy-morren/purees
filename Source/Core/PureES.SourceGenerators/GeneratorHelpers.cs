using System.Diagnostics;
using System.Runtime.CompilerServices;
using PureES.SourceGenerators.Framework;

namespace PureES.SourceGenerators;

internal static class GeneratorHelpers
{
    public static void WriteGetElapsed(IndentedWriter writer, bool includeTimespan)
    {
        var sw = $"global::{typeof(Stopwatch).FullName}";
        var ts = $"global::{typeof(TimeSpan).FullName}";
        var frequency = $"{sw}.{nameof(Stopwatch.Frequency)}";

        const string ifNet7 = "#if NET7_0_OR_GREATER";
        const string elapsedTime = "GetElapsedTime";
        
        //Method to get elapsed time in milliseconds
        writer.WriteMethodAttributes(MethodImplOptions.AggressiveInlining);
        writer.WriteStatement("private static double GetElapsed(long start)", () =>
        {
            writer.WriteRawLine(ifNet7);
            writer.WriteLine($"return {sw}.{elapsedTime}(start).{nameof(TimeSpan.TotalMilliseconds)};");
            writer.WriteRawLine("#else");
            writer.WriteLine($"return ({GetTimestamp} - start) * 1000 / (double){frequency};");
            writer.WriteRawLine("#endif");
        });

        if (!includeTimespan) return;
        //Method to get elapsed TimeSpan
        
        writer.WriteMethodAttributes(MethodImplOptions.AggressiveInlining);
        writer.WriteStatement("private static TimeSpan GetElapsedTimespan(long start)", () =>
        {
            writer.WriteRawLine(ifNet7);
            writer.WriteLine($"return {sw}.{elapsedTime}(start);");
            writer.WriteRawLine("#else");
            writer.WriteLine(
                $"return {ts}.{nameof(TimeSpan.FromSeconds)}(({GetTimestamp} - start) / (double){frequency});");
            writer.WriteRawLine("#endif");
        });

    }

    public static string GetTimestamp => $"global::{typeof(Stopwatch).FullName}.{nameof(Stopwatch.GetTimestamp)}()";

    /// <summary>
    /// Throws an <see cref="ArgumentNullException"/> if the value is null
    /// </summary>
    public static void CheckNotNull(this IndentedWriter writer, string value, string? parameterName = null)
    {
        parameterName ??= value;

        const string exception = $"global::System.{nameof(ArgumentNullException)}";
        //Use ArgumentNullException.ThrowIfNull for .NET 6 and later
        writer.WriteRawLine("#if NET6_0_OR_GREATER");
        writer.WriteLine($"{exception}.ThrowIfNull({value}, nameof({parameterName}));");
        writer.WriteRawLine("#else");
        writer.WriteLine($"if ({value} is null) throw new {exception}(nameof({parameterName}));");
        writer.WriteRawLine("#endif");
    }
}