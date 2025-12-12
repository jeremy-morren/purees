namespace PureES.SourceGenerators;

/// <summary>
/// Map of types from outside assembly
/// </summary>
/// <remarks>
/// Any external references other than core types fail during generation for some reason.
/// This is an inelegant workaround
/// </remarks>
[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class ExternalTypes
{
    public const string IAsyncEnumerableBase = "System.Collections.Generic.IAsyncEnumerable";
    
    public static string IAsyncEnumerable(string tOut) => $"global::{IAsyncEnumerableBase}<{tOut}>";
    public static string IAsyncEnumerator(string tOut) => $"global::System.Collections.Generic.IAsyncEnumerator<{tOut}>";
    
    public const string LoggingNamespace = "Microsoft.Extensions.Logging";
    
    public static string ILogger(string context) => $"global::{LoggingNamespace}.ILogger<{context}>";
    
    public static string NullLoggerInstance(string context) => $"global::{LoggingNamespace}.Abstractions.NullLogger<{context}>.Instance";

    public const string LogLevel = $"global::{LoggingNamespace}.LogLevel";
    
    public const string DINamespace = "Microsoft.Extensions.DependencyInjection";

    public const string OptionsNamespace = "Microsoft.Extensions.Options";
    
    public static string IOptions(string type) => $"global::{OptionsNamespace}.IOptions<{type}>";
}