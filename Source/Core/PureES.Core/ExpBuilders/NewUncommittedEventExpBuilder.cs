using PureES.Core.EventStore;

// ReSharper disable MemberCanBeMadeStatic.Global

namespace PureES.Core.ExpBuilders;

internal class NewUncommittedEventExpBuilder
{
    private readonly CommandHandlerBuilderOptions _options;

    public NewUncommittedEventExpBuilder(CommandHandlerBuilderOptions options) => _options = options;

    public Expression New(Expression @event, Expression metadata)
    {
        //As Guid.NewGuid()
        var method = typeof(Guid).GetMethod(nameof(Guid.NewGuid)) ??
                     throw new InvalidOperationException("Could not get Guid.NewGuid() method");
        return New(Expression.Call(method), @event, metadata);
    }

    public Expression New(Expression eventId, Expression @event, Expression metadata)
    {
        //Looks like new UncommittedEvent(eventId, streamId, @event, metadata)
        var constructor = typeof(UncommittedEvent).GetConstructors()
                              .SingleOrDefault(c => c.GetParameters().Length == 3)
                          ?? throw new InvalidOperationException("Unable to get UncommittedEvent constructor");
        if (eventId.Type != typeof(Guid))
            throw new InvalidOperationException("Invalid EventId expression");
        //TODO: Check event is not null
        if (@event.Type != typeof(object)) //Cast if necessary
            @event = Expression.Convert(@event, typeof(object));
        if (metadata.Type != typeof(object))
            metadata = Expression.Convert(metadata, typeof(object));
        return Expression.New(constructor, eventId, @event, metadata);
    }
}