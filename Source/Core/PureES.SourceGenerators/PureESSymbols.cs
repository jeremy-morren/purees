// ReSharper disable InconsistentNaming

namespace PureES.SourceGenerators;

[PublicAPI]
internal static class PureESSymbols
{
    public const string UncommittedEvent = "PureES.UncommittedEvent";
    public const string UncommittedEventsList = "PureES.UncommittedEventsList";
    public const string EventEnvelope = "PureES.EventEnvelope";
    public const string CommandResult = "PureES.CommandResult";
    public const string EventsTransaction = "PureES.EventsTransaction";
    
    public const string CommandAttribute = "PureES.CommandAttribute";
    public const string EventHandlersAttribute = "PureES.EventHandlersAttribute";
    public const string AggregateAttribute = "PureES.AggregateAttribute";
    public const string EventAttribute = "PureES.EventAttribute";
    public const string StreamIdAttribute = "PureES.StreamIdAttribute";
    
    public const string IEventStore = "PureES.IEventStore";
    public const string ICommandHandler = "PureES.ICommandHandler";
    public const string IConcurrency = "PureES.IConcurrency";
    public const string ICommandStreamId = "PureES.ICommandStreamId";
    public const string ICommandValidator = "PureES.ICommandValidator";
    public const string IAsyncCommandValidator = "PureES.IAsyncCommandValidator";
    public const string IAggregateFactory = "PureES.IAggregateFactory";
    public const string IAggregateStore = "PureES.IAggregateStore";
    public const string IEventEnricher = "PureES.IEventEnricher";
    public const string IAsyncEventEnricher = "PureES.IAsyncEventEnricher";
    public const string IEventHandler = "PureES.IEventHandler";
    
    public const string Options = "PureES.PureESOptions";
    public const string EventHandlerOptions = "PureES.PureESEventHandlerOptions";
    
    public const string RehydratedAggregate = "PureES.RehydratedAggregate";
    public const string RehydrationException = "PureES.RehydrationException";

    public const string BasicEventTypeMap = "PureES.BasicEventTypeMap";
}