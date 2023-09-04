using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using PureES.Core.Generators.Framework;

namespace PureES.Core.Tests.Framework;

[PublicAPI]
public class FakeErrorLog : IErrorLog
{
    private readonly List<(string Id, string Title, string MessageFormat, string Message)> _errors = new();
    
    public IReadOnlyList<(string Id, string Title, string MessageFormat, string Message)> Errors => _errors.AsReadOnly();
    
    public void WriteError(Location location, string id, string title, string messageFormat, params object?[] messageArgs)
    {
        _errors.Add((id, title, messageFormat, string.Format(messageFormat, messageArgs)));
    }
}