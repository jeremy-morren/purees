using System.Diagnostics;
using Microsoft.ApplicationInsights.DataContracts;
using PureES.SourceGenerators.Framework;

namespace PureES.SourceGenerators;

/// <summary>
/// Helper class for Application Insights
/// </summary>
internal static class AppInsightsHelpers
{
    public const string AppInsightsClient = "global::Microsoft.ApplicationInsights.TelemetryClient";
    private const string AppInsightsEvent = "global::Microsoft.ApplicationInsights.DataContracts.EventTelemetry";

    /// <summary>
    /// Track an event in Application Insights.
    /// </summary>
    /// <paramref name="writer">Indented writer</paramref>
    /// <paramref name="properties">Event properties. Key is property name, value is c# to get value</paramref>
    /// <remarks>
    /// Assumes a field name <c>_telemetryClient</c> and an activity called <c>activity</c>
    /// </remarks>
    public static void TrackEvent(IndentedWriter writer, Dictionary<string, string?> properties)
    {
        writer.WriteLine("activity.Stop();"); // Stop the activity so we get the duration

        writer.WriteStatement("if (_telemetryClient != null)", () =>
        {
            writer.WriteLine($"var telemetry = new {AppInsightsEvent}()");
            writer.PushBrace();

            writer.WriteLine($"Name = activity.{nameof(Activity.Source)}.{nameof(ActivitySource.Name)},");
            writer.WriteLine($"Timestamp = activity.{nameof(Activity.StartTimeUtc)},");
            writer.WriteStatement("Metrics", () =>
            {
                writer.WriteLine(
                    $"{{ \"duration\", activity.{nameof(Activity.Duration)}.{nameof(TimeSpan.TotalMilliseconds)} }},");
            });
            writer.WriteStatement("Properties = ", () =>
            {

                foreach (var p in properties)
                    if (p.Value != null)
                        writer.WriteLine($"{{ {p.Key.ToStringLiteral()}, {p.Value} }},");
            });
            writer.Pop();
            writer.WriteLine("};");
            writer.WriteLine("_telemetryClient.TrackEvent(telemetry);");
        });
    }
}