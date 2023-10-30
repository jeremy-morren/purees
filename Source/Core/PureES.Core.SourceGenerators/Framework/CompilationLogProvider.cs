using Microsoft.CodeAnalysis;

namespace PureES.Core.SourceGenerators.Framework;

internal class CompilationLogProvider : IErrorLog
{
    private readonly SourceProductionContext _context;

    public CompilationLogProvider(SourceProductionContext context) => _context = context;

    public void WriteError(Location location,
        string id,
        string title,
        string messageFormat,
        params object?[] messageArgs)
    {
        var descriptor = new DiagnosticDescriptor($"ES{id}",
            title,
            messageFormat,
            "PureES.SourceGenerator",
            DiagnosticSeverity.Error,
            true);
        _context.ReportDiagnostic(Diagnostic.Create(descriptor, location, messageArgs));
    }
}