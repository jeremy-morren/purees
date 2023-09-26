﻿using Microsoft.CodeAnalysis;
using PureES.Core.Generators.Framework;

namespace PureES.Core.Generators;

internal class PureESErrorLogWriter
{
    private readonly IErrorLog _log;

    public PureESErrorLogWriter(IErrorLog log)
    {
        _log = log;
    }

    private void WriteError(Location location,
        string id,
        string title,
        string messageFormat,
        params object?[] messageArgs)
    {
        ErrorCount++;
        _log.WriteError(location, id, title, messageFormat, messageArgs);
    }

    public int ErrorCount { get; private set; }
    
    public void MultipleParametersDefinedWithAttribute(IMethod method, Type attribute)
    {
        WriteError(method.Location,
            "1010",
            "Multiple method parameters decorated with attribute",
            "Method '{0}' from '{1}' has multiple parameters decorated with '{2}'",
            method, method.DeclaringType, attribute.FullName);
    }
    
    public void UnknownOrDuplicateParameter(IMethod method, IParameter parameter)
    {
        WriteError(parameter.Location,
            "1010",
            "Unknown or duplicate method parameter",
            "Parameter '{0}' on method '{1}' from '{2}' is unknown or duplicated",
            parameter.Name, method, method.DeclaringType);
    }

    public void HandlerReturnsVoid(IMethod method)
    {
        WriteError(method.Location,
            "1011",
            "Command handler returns void",
            "Command handler '{0}' from '{1}' returns void",
            method);
    }
    
    public void HandlerReturnsNonGenericAsync(IMethod method)
    {
        WriteError(method.Location,
            "1013",
            "Command handler returns non-generic Task or ValueTask",
            "Command handler '{0}' from '{1}' returns non-generic Task or ValueTask",
            method);
    }
    
    public void MultipleEventEnvelopeParameters(IMethod method)
    {
        WriteError(method.Location,
            "1020",
            "Multiple event envelope parameters on method",
            "Method '{0}' from '{1}' has multiple event envelope parameters",
            method, method.DeclaringType);
    }
    
    public void InvalidStaticWhenReturnType(IMethod method)
    {
        WriteError(method.Location,
            "1021",
            "Invalid return type on static when method",
            "Static when method '{0}' on '{1}' does not return parent aggregate",
            method, method.DeclaringType);
    }

    public void EventHandlerMethodHasNoParent(IMethod method)
    {
        WriteError(method.Location,
            "1030",
            "Declaring type for event handler method is null",
            "Declaring type for method '{0}' is null",
            method);
    }

    public void UnknownEventHandlerEventType(IMethod method)
    {
        WriteError(method.Location,
            "1031",
            "Unable to determine event type for event handler",
            "Unable to determine event type for event handler method '{0}'",
            method);
    }
}