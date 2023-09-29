using PureES.Core.Generators.Framework;
using PureES.Core.Generators.Models;
using EventHandler = PureES.Core.Generators.Models.EventHandler;

// ReSharper disable InvertIf

namespace PureES.Core.Generators;

internal class EventHandlersBuilder : BuilderBase
{

    private EventHandlersBuilder(IErrorLog log) : base(log)
    {
    }

    public static bool BuildEventHandlers(IType eventHandlersType, out List<EventHandler> handlers, IErrorLog log) =>
        new EventHandlersBuilder(log).BuildEventHandler(eventHandlersType, out handlers);

    private bool BuildEventHandler(IType eventHandlersType, out List<EventHandler> handlers)
    {
        handlers = new List<EventHandler>();

        if (eventHandlersType.IsGenericType)
        {
            Log.EventHandlersCannotBeGenericType(eventHandlersType);
            return false;
        }

        foreach (var method in eventHandlersType.GetMethodsRecursive().Where(m => m.IsPublic))
        {
            //TODO: Check for generic parameters
            
            var services = method.Parameters
                .Where(p => p.HasFromServicesAttribute())
                .Select(p => p.Type)
                .ToArray();
            
            if (method.Parameters.Any(p => p.Type.IsEventEnvelope()))
            {
                if (!ValidateAllParameters(method, p => p.Type.IsEventEnvelope()))
                    continue;
                if (!ValidateSingleEventEnvelope(method, out var @event))
                    continue;
                handlers.Add(new EventHandler()
                {
                    Parent = eventHandlersType,
                    Event = @event,
                    Method = method,
                    Services = services
                });
            }
            else if (method.Parameters.Any(p => p.HasAttribute<EventAttribute>()))
            {
                if (!ValidateSingleAttribute<EventAttribute>(method, out var @event))
                    continue;
                handlers.Add(new EventHandler()
                {
                    Parent = eventHandlersType,
                    Event = @event.Type,
                    Method = method,
                    Services = services
                });
            }
            
            //Unknown method, ignore
        }

        return Log.ErrorCount == 0;
    }
}