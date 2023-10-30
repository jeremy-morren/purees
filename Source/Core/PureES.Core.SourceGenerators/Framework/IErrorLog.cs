using Microsoft.CodeAnalysis;

namespace PureES.Core.SourceGenerators.Framework;

internal interface IErrorLog
{
    void WriteError(Location location,
        string id,
        string title,
        string messageFormat,
        params object?[] messageArgs);
}