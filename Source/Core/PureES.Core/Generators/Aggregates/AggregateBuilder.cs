using PureES.Core.Generators.Aggregates.Models;
using PureES.Core.Generators.Framework;

namespace PureES.Core.Generators.Aggregates;

internal class AggregateBuilder
{
    private readonly AggregatesErrorLogWriter _log;

    private AggregateBuilder(IErrorLog log)
    {
        _log = new AggregatesErrorLogWriter(log);
    }

    public static bool Build(IType type, out Aggregate aggregate, IErrorLog log) =>
        new AggregateBuilder(log).BuildInternal(type, out aggregate);

    private bool BuildInternal(IType type, out Aggregate aggregate)
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
                    Services = services,
                    IsAsync = method.ReturnType.IsAsync(out _),
                    ReturnType = returnType,
                    EventType = returnType.IsCommandResultType(out var eventType, out _) ? eventType : returnType,
                    ResultType = returnType.IsCommandResultType(out _, out var resultType) ? resultType : null,
                });
            }
            //First, check for parameter with EventAttribute
            if (method.Parameters.Any(p => p.HasAttribute<EventAttribute>()))
            {
                //When method with EventAttribute
                if (!ValidateSingleAttribute<EventAttribute>(method, out var e))
                    continue;
                if (!ValidateAllParameters(method, p => p.HasAttribute<EventAttribute>()))
                    continue;
                if (method is { IsStatic: true, ReturnType: null })
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
            }
            if (method.Parameters.Any(p => p.Type.IsEventEnvelope()))
            {
                //Method has an EventEnvelope parameter
                if (!ValidateSingleEventEnvelope(method, out var @event))
                    continue;
                if (!ValidateAllParameters(method, p => p.Type.IsEventEnvelope()))
                    continue;
                if (method is { IsStatic: true, ReturnType: null })
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
                if (list[0].Type.IsGenericEventEnvelope(out var e))
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
}