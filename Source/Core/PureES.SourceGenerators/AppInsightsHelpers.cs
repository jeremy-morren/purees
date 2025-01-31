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
    public static void TrackEvent(IndentedWriter writer, string name, Dictionary<string, string?> properties)
    {
        writer.WriteLine("activity.Stop();"); // Stop the activity so we get the duration

        writer.WriteStatement("if (_telemetryClient != null)", () =>
        {
            WriteStatement($"var telemetry = new {AppInsightsEvent}()", ';', () =>
            {
                writer.WriteLine($"Name = {name.ToStringLiteral()},");
                writer.WriteLine("Timestamp = activity.StartTimeUtc,");

                WriteStatement("Metrics = ", ',', () => writer.WriteLine(
                    $"{{ \"duration\", activity.Duration.{nameof(TimeSpan.TotalMilliseconds)} }},"));

                WriteStatement("Properties = ", ',', () =>
                {
                    foreach (var p in properties)
                        if (p.Value != null)
                            writer.WriteLine($"{{ {p.Key.ToStringLiteral()}, {p.Value} }},");
                });
            });
            writer.WriteLine("_telemetryClient.TrackEvent(telemetry);");
        });

        return;

        void WriteStatement(string header, char endChar, Action action)
        {
            writer.WriteLine(header);
            writer.PushBrace();
            action();
            writer.Pop();
            writer.WriteLine($"}}{endChar}");
        }
    }
}