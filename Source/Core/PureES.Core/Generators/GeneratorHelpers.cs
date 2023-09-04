using System.Diagnostics;
using PureES.Core.Generators.Framework;

namespace PureES.Core.Generators;

internal static class GeneratorHelpers
{
    public static void WriteGetElapsed(IndentedWriter writer)
    {
        //Method to get elapsed time
        var ts = $"global::{typeof(TimeSpan).FullName}";
        
        writer.WriteMethodAttributes();
        writer.WriteStatement("private static double GetElapsed(long start)", 
            $"return ({GetTimestamp} - start) * 1000 / (double)Stopwatch.Frequency;");
    }

    public static string GetTimestamp => $"global::{typeof(Stopwatch).FullName}.{nameof(Stopwatch.GetTimestamp)}()";
}