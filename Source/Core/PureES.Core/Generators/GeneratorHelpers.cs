using System.Diagnostics;
using PureES.Core.Generators.Framework;

namespace PureES.Core.Generators;

internal static class GeneratorHelpers
{
    public static void WriteGetElapsed(IndentedWriter writer, bool includeTimespan)
    {
        //Method to get elapsed time
        var ts = $"global::{typeof(TimeSpan).FullName}";
        var frequency = $"global::{typeof(Stopwatch).FullName}.{nameof(Stopwatch.Frequency)}";
        
        writer.WriteMethodAttributes();
        writer.WriteStatement("private static double GetElapsed(long start)", 
            $"return ({GetTimestamp} - start) * 1000 / (double){frequency};");

        if (!includeTimespan) return;
        writer.WriteMethodAttributes();
        writer.WriteStatement("private static TimeSpan GetElapsedTimespan(long start)", 
            $"return {ts}.{nameof(TimeSpan.FromSeconds)}(({GetTimestamp} - start) / (double){frequency});");
    }

    public static string GetTimestamp => $"global::{typeof(Stopwatch).FullName}.{nameof(Stopwatch.GetTimestamp)}()";
}