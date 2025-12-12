using Microsoft.CodeAnalysis;

namespace PureES.SourceGenerators.Framework;

internal interface IErrorLog
{
    void WriteError(Location location,
        string id,
        string title,
        string messageFormat,
        params object?[] messageArgs);
}