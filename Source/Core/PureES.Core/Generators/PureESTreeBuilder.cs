using PureES.Core.Generators.Framework;
using PureES.Core.Generators.Models;
using EventHandler = PureES.Core.Generators.Models.EventHandler;

// ReSharper disable InvertIf

namespace PureES.Core.Generators;

internal class PureESTreeBuilder
{
    private readonly PureESErrorLogWriter _log;

    private PureESTreeBuilder(IErrorLog log)
    {
        _log = new PureESErrorLogWriter(log);
    }

    public static bool BuildAggregate(IType type, out Aggregate aggregate, IErrorLog log)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        return new PureESTreeBuilder(log).BuildAggregateInternal(type, out aggregate);
    }

    private bool BuildAggregateInternal(IType type, out Aggregate aggregate)
    {
        aggregate = null!;
        var handlers = new List<Handler>();
        var when = new List<When>();

        foreach (var method in type.Methods)
        {
            var services = method.Parameters
                .Where(p => p.HasFromServicesAttribute())
                .Select(p => p.Type)
                .ToArray();
            
            if (method.Parameters.Any(p => p.HasAttribute<CommandAttribute>()))
            {
                //Command
                if (!ValidateSingleAttribute<CommandAttribute>(method, out var command))
                    continue;
                if (!ValidateAllParameters(method, p => p.HasAttribute<CommandAttribute>()))
                    continue;
                if (method.ReturnType == null)
                {
                    _log.HandlerReturnsVoid(method);
                    continue;
                }

                var returnType = method.ReturnType;
                if (method.ReturnType.IsAsync(out var underlyingType))
                {
                    if (underlyingType == null)
                    {
                        _log.HandlerReturnsNonGenericAsync(method);
                        continue;
                    }
                    returnType = underlyingType;
                }

                handlers.Add(new Handler()
                {
                    Command = command.Type,
                    Method = method,
                    StreamId = command.Type.Attributes.FirstOrDefault(a => a.Is<StreamIdAttribute>())?.StringParameter,
                    Services = services,
                    IsAsync = method.ReturnType.IsAsync(out _),
                    ReturnType = returnType,
                    EventType = returnType.IsCommandResultType(out var eventType, out _) ? eventType : returnType,
                    ResultType = returnType.IsCommandResultType(out _, out var resultType) ? resultType : null,
                });
                continue;
            }
            //First, check for parameter with EventAttribute
            if (method.Parameters.Any(p => p.HasAttribute<EventAttribute>()))
            {
                //When method with EventAttribute
                if (!ValidateSingleAttribute<EventAttribute>(method, out var e))
                    continue;
                if (!ValidateAllParameters(method, p => p.HasAttribute<EventAttribute>()))
                    continue;
                if (method.IsStatic && !ValidateReturnType(method, aggregate.Type))
                {
                    _log.InvalidCreateWhenReturnType(method);
                    continue;
                }
                when.Add(new When()
                {
                    Event = e.Type,
                    Method = method,
                    Services = services
                });
                continue;
            }
            if (method.Parameters.Any(p => p.Type.IsEventEnvelope()))
            {
                //Method has an EventEnvelope parameter
                if (!ValidateSingleEventEnvelope(method, out var @event))
                    continue;
                if (!ValidateAllParameters(method, p => p.Type.IsEventEnvelope()))
                    continue;
                if (method.IsStatic && !ValidateReturnType(method, type))
                {
                    _log.InvalidCreateWhenReturnType(method);
                    continue;
                }
                when.Add(new When()
                {
                    Event = @event,
                    Method = method,
                    Services = services
                });
                continue;
            }
            //Don't know what the method is, ignore
        }

        if (_log.ErrorCount > 0)
            return false;
        aggregate = new Aggregate()
        {
            Type = type,
            Handlers = handlers.ToArray(),
            When = when.ToArray(),
        };
        return true;
    }
    
    public static bool BuildEventHandler(IMethod method, out EventHandler handler, IErrorLog log) =>
        new PureESTreeBuilder(log).BuildEventHandler(method, out handler);

    private bool BuildEventHandler(IMethod method, out EventHandler handler)
    {
        handler = null!;

        if (method.DeclaringType == null)
        {
            _log.EventHandlerMethodHasNoParent(method);
            return false;
        }

        var services = method.Parameters
            .Where(p => p.HasFromServicesAttribute())
            .Select(p => p.Type)
            .ToArray();

        if (method.Parameters.Any(p => p.HasAttribute<EventAttribute>()))
        {
            if (!ValidateSingleAttribute<EventAttribute>(method, out var @event))
                return false;
            handler = new EventHandler()
            {
                Parent = method.DeclaringType,
                Event = @event.Type,
                Method = method,
                Services = services
            };
            return true;
        }

        if (method.Parameters.Any(p => p.Type.IsGenericEventEnvelope()))
        {
            if (!ValidateAllParameters(method, p => p.Type.IsGenericEventEnvelope()))
                return false;
            if (!ValidateSingleEventEnvelope(method, out var @event))
                return false;
            handler = new EventHandler()
            {
                Parent = method.DeclaringType,
                Event = @event!,
                Method = method,
                Services = services
            };
            return true;
        }
        //Unknown method
        throw new NotImplementedException("Unknown event handler");
    }
    
    #region Helpers

    /// <summary>
    /// Validates that the attribute only occurs on 1 parameter
    /// </summary>
    /// <returns></returns>
    private bool ValidateSingleAttribute<TAttribute>(IMethod method, out IParameter parameter)
    {
        var list = method.Parameters.Where(p => p.HasAttribute<TAttribute>()).ToList();
        switch (list.Count)
        {
            case 0:
                throw new NotImplementedException();
            case 1:
                parameter = list[0];
                return true;
            default:
                _log.MultipleParametersDefinedWithAttribute(method, typeof(TAttribute));
                parameter = null!;
                return false;
        }
    }

    private bool ValidateSingleEventEnvelope(IMethod method, out IType? @event)
    {
        @event = null;
        var list = method.Parameters.Where(p => p.Type.IsEventEnvelope()).ToList();
        switch (list.Count)
        {
            case 0:
                throw new NotImplementedException();
            case 1:
                if (list[0].Type.IsGenericEventEnvelope(out var e, out _))
                    @event = e;
                return true;
            default:
                _log.MultipleEventEnvelopeParameters(method);
                return false;
        }
    }

    /// <summary>
    /// Validates that all parameters match a given predicate
    /// </summary>
    private bool ValidateAllParameters(IMethod method, Func<IParameter, bool> validate)
    {
        var valid = true;
        foreach (var p in method.Parameters)
        {
            //These 2 conditions are always valid
            if (p.HasFromServicesAttribute() || p.Type.IsCancellationToken())
                continue;
            if (validate(p))
                continue;
            _log.UnknownOrDuplicateParameter(method, p);
            valid = false;
        }
        return valid;
    }

    private static bool ValidateReturnType(IMethod method, IType expected)
    {
        if (method.ReturnType == null) return false;
        if (method.ReturnType.IsAsync(out var underlyingType))
            return underlyingType != null && expected.Equals(underlyingType);
        return method.ReturnType.Equals(expected);
    }
    
    #endregion
}