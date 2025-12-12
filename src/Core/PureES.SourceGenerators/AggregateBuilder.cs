using PureES.SourceGenerators.Framework;
using PureES.SourceGenerators.Models;

// ReSharper disable InvertIf

namespace PureES.SourceGenerators;

internal class AggregateBuilder : BuilderBase
{
    private AggregateBuilder(IErrorLog log) : base(log)
    {
    }

    public static bool BuildAggregate(IType type, out Aggregate aggregate, IErrorLog log)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        return new AggregateBuilder(log).BuildAggregateInternal(type, out aggregate);
    }

    private bool BuildAggregateInternal(IType aggregateType, out Aggregate aggregate)
    {
        aggregate = null!;
        var handlers = new List<CommandHandler>();
        var when = new List<When>();

        if (aggregateType.IsGenericType)
        {
            Log.AggregateCannotBeGenericType(aggregateType);
            return false;
        }
        
        foreach (var method in aggregateType.GetMethodsRecursive())
        {
            if (method.Name is "ToString" or "GetHashCode" or "Equals" or "Deconstruct")
                continue;
            
            var services = method.Parameters
                .Where(p => p.HasFromServicesAttribute())
                .Select(p => p.Type)
                .ToArray();
            
            var isUpdate = !method.IsStatic || method.Parameters.Any(p => p.Type.Equals(aggregateType));
            
            if (method.Parameters.Any(p => p.HasCommandAttribute()))
            {
                //Command
                if (!ValidateSingleAttribute(method, PureESSymbols.CommandAttribute, out var command))
                    continue;
                if (!ValidateAllParameters(method, p => p.HasCommandAttribute() || p.Type.Equals(aggregateType)))
                    continue;
                if (method.ReturnType == null)
                {
                    Log.HandlerReturnsVoid(method);
                    continue;
                }

                var returnType = method.ReturnType;
                if (method.ReturnType.IsAsync(out var underlyingType))
                {
                    if (underlyingType == null)
                    {
                        Log.HandlerReturnsNonGenericAsync(method);
                        continue;
                    }
                    returnType = underlyingType;
                }

                handlers.Add(new CommandHandler()
                {
                    Command = command.Type,
                    IsUpdate = isUpdate,
                    Method = method,
                    StreamId = command.Type.Attributes.FirstOrDefault(a => a.IsStreamId())?.StringParameter,
                    Services = services,
                    IsAsync = method.ReturnType.IsAsync(out _),
                    ReturnType = returnType,
                    EventType = returnType.IsCommandResultType(out var eventType, out _) ? eventType : returnType,
                    ResultType = returnType.IsCommandResultType(out _, out var resultType) ? resultType : null,
                });
                continue;
            }
            //Method must be when or unknown
            
            //Check for methods with IEventEnvelope parameter
            if (method.Parameters.Any(p => p.Type.IsEventEnvelope()))
            {
                //Method has an IEventEnvelope parameter
                if (!ValidateSingleEventEnvelope(method, out var @event))
                    continue;
                //All parameters must be IEventEnvelope or the aggregate type
                if (!ValidateAllParameters(method, p => p.Type.IsEventEnvelope() || p.Type.Equals(aggregateType)))
                    continue;
                //If method is static, the return type must be the aggregate type
                if (method.IsStatic && !ValidateReturnType(method, aggregateType))
                {
                    Log.InvalidStaticWhenReturnType(method);
                    continue;
                }
                when.Add(new When()
                {
                    Event = @event,
                    IsUpdate = isUpdate,
                    Method = method,
                    Services = services
                });
                continue;
            }
            
            //check for parameter with EventAttribute
            if (method.Parameters.Any(p => p.HasEventAttribute()))
            {
                //When method with EventAttribute

                if (!ValidateSingleAttribute(method, PureESSymbols.EventAttribute, out var e))
                    continue;
                //All parameters must have EventAttribute or be the aggregate type
                if (!ValidateAllParameters(method, p => p.HasEventAttribute() || p.Type.Equals(aggregateType)))
                    continue;
                if (method.IsStatic && !ValidateReturnType(method, aggregateType))
                {
                    Log.InvalidStaticWhenReturnType(method);
                    continue;
                }
                when.Add(new When()
                {
                    Event = e.Type,
                    IsUpdate = isUpdate,
                    Method = method,
                    Services = services
                });
                continue;
            }

            //Don't know what the method is, ignore
        }

        if (Log.ErrorCount > 0)
            return false;
        aggregate = new Aggregate()
        {
            Type = aggregateType,
            Handlers = handlers.ToArray(),
            When = when.ToArray(),
        };
        return true;
    }
}