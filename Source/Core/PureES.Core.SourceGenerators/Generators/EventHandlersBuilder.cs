using PureES.Core.SourceGenerators.Framework;

// ReSharper disable InvertIf

namespace PureES.Core.SourceGenerators.Generators;

internal class EventHandlersBuilder : BuilderBase
{

    private EventHandlersBuilder(IErrorLog log) : base(log)
    {
    }

    public static bool BuildEventHandlers(IType eventHandlersType, out List<Models.EventHandler> handlers, IErrorLog log) =>
        new EventHandlersBuilder(log).BuildEventHandler(eventHandlersType, out handlers);

    private bool BuildEventHandler(IType eventHandlersType, out List<Models.EventHandler> handlers)
    {
        handlers = new List<Models.EventHandler>();

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
                handlers.Add(new Models.EventHandler()
                {
                    Parent = eventHandlersType,
                    EventType = @event,
                    Method = method,
                    Services = services
                });
            }
            else if (method.Parameters.Any(p => p.HasAttribute<EventAttribute>()))
            {
                if (!ValidateSingleAttribute<EventAttribute>(method, out var @event))
                    continue;
                handlers.Add(new Models.EventHandler()
                {
                    Parent = eventHandlersType,
                    EventType = @event.Type,
                    Method = method,
                    Services = services
                });
            }
            
            //Unknown method, ignore
        }

        return Log.ErrorCount == 0;
    }
}