﻿//HintName: PureES.CommandHandlers.CommandHandler.g.cs
// <auto-generated/>
// This file was automatically generated by the PureES source generator.
// Do not edit this file manually since it will be automatically overwritten.
// ReSharper disable All

#nullable disable

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PureES.CommandHandlers
{
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("PureES.SourceGenerator", "1.0.0.0")]
    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
    internal class CommandHandler : global::PureES.Core.ICommandHandler<int[]>
    {
        private readonly global::PureES.Core.PureESStreamId<int[]> _getStreamId;
        private readonly global::PureES.Core.IAggregateStore<global::PureES.Core.Tests.Models.TestAggregates.Aggregate> _aggregateStore;
        private readonly global::PureES.Core.EventStore.IEventStore _eventStore;
        private readonly global::PureES.Core.IOptimisticConcurrency _concurrency;
        private readonly global::System.Collections.Generic.IEnumerable<global::PureES.Core.IEventEnricher> _enrichers;
        private readonly global::System.Collections.Generic.IEnumerable<global::PureES.Core.IAsyncEventEnricher> _asyncEnrichers;
        private readonly global::System.Collections.Generic.IEnumerable<global::PureES.Core.ICommandValidator<int[]>> _syncValidators;
        private readonly global::System.Collections.Generic.IEnumerable<global::PureES.Core.IAsyncCommandValidator<int[]>> _asyncValidators;
        private readonly global::Microsoft.Extensions.Logging.ILogger<CommandHandler> _logger;
        private readonly global::Microsoft.Extensions.Logging.ILoggerFactory _service0;

        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public CommandHandler(
            global::Microsoft.Extensions.Logging.ILoggerFactory service0,
            global::PureES.Core.PureESStreamId<int[]> getStreamId,
            global::PureES.Core.EventStore.IEventStore eventStore,
            global::PureES.Core.IAggregateStore<global::PureES.Core.Tests.Models.TestAggregates.Aggregate> aggregateStore,
            global::PureES.Core.IOptimisticConcurrency concurrency = null,
            global::System.Collections.Generic.IEnumerable<global::PureES.Core.IEventEnricher> enrichers = null,
            global::System.Collections.Generic.IEnumerable<global::PureES.Core.IAsyncEventEnricher> asyncEnrichers = null,
            global::System.Collections.Generic.IEnumerable<global::PureES.Core.ICommandValidator<int[]>> syncValidators = null,
            global::System.Collections.Generic.IEnumerable<global::PureES.Core.IAsyncCommandValidator<int[]>> asyncValidators = null,
            global::Microsoft.Extensions.Logging.ILogger<CommandHandler> logger = null)
        {
            this._getStreamId = getStreamId ?? throw new ArgumentNullException(nameof(getStreamId));
            this._eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
            this._aggregateStore = aggregateStore ?? throw new ArgumentNullException(nameof(aggregateStore));
            this._concurrency = concurrency;
            this._enrichers = enrichers;
            this._asyncEnrichers = asyncEnrichers;
            this._syncValidators = syncValidators;
            this._asyncValidators = asyncValidators;
            this._logger = logger;
            this._service0 = service0 ?? throw new ArgumentNullException(nameof(service0));
        }



        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        private static double GetElapsed(long start)
        {
            return (global::System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000 / (double)global::System.Diagnostics.Stopwatch.Frequency;
        }


        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public async global::System.Threading.Tasks.Task<ulong> Handle(int[] command, CancellationToken cancellationToken)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }
            var commandType = typeof(int[]);
            var aggregateType = typeof(global::PureES.Core.Tests.Models.TestAggregates.Aggregate);
            this._logger?.Log(
                logLevel: global::Microsoft.Extensions.Logging.LogLevel.Debug,
                exception: null,
                message: "Handling command {@Command}. Aggregate: {@Aggregate}. Method: {Method}",
                commandType,
                aggregateType,
                "CreateOnAsyncEnumerable");
            var start = global::System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                if (this._syncValidators != null)
                {
                    foreach (var validator in this._syncValidators)
                    {
                        validator.Validate(command);
                    }
                }
                if (this._asyncValidators != null)
                {
                    foreach (var validator in this._asyncValidators)
                    {
                        await validator.Validate(command, cancellationToken);
                    }
                }
                var streamId = this._getStreamId.GetId(command);
                var result = global::PureES.Core.Tests.Models.TestAggregates.Aggregate.CreateOnAsyncEnumerable(command, this._service0);
                var revision = currentRevision;
                if (result != null)
                {
                    var events = new List<global::PureES.Core.UncommittedEvent>();
                    await foreach (var e in result.WithCancellation(cancellationToken))
                    {
                        events.Add(new global::PureES.Core.UncommittedEvent() { Event = e });
                    }
                    if (events.Count > 0)
                    {
                        if (this._enrichers != null)
                        {
                            foreach (var enricher in this._enrichers)
                            {
                                foreach (var e in events)
                                {
                                    enricher.Enrich(e);
                                }
                            }
                        }
                        if (this._asyncEnrichers != null)
                        {
                            foreach (var enricher in this._asyncEnrichers)
                            {
                                foreach (var e in events)
                                {
                                    await enricher.Enrich(e, cancellationToken);
                                }
                            }
                        }
                        revision = await _eventStore.Create(streamId, events, cancellationToken);
                    }
                }
                this._concurrency?.OnUpdated(streamId, command, null, revision);
                this._logger?.Log(
                    logLevel: global::Microsoft.Extensions.Logging.LogLevel.Information,
                    exception: null,
                    message: "Handled command {@Command}. Elapsed: {Elapsed:0.0000}ms. Stream {StreamId} is now at {Revision}. Aggregate: {@Aggregate}. Method: {Method}",
                    commandType,
                    GetElapsed(start),
                    streamId,
                    revision,
                    aggregateType,
                    "CreateOnAsyncEnumerable");
                return revision;
            }
            catch (global::System.Exception ex)
            {
                this._logger?.Log(
                    logLevel: global::Microsoft.Extensions.Logging.LogLevel.Information,
                    exception: ex,
                    message: "Error handling command {@Command}. Aggregate: {@Aggregate}. Method: {Method}. Elapsed: {Elapsed:0.0000}ms",
                    commandType,
                    aggregateType,
                    "CreateOnAsyncEnumerable",
                    GetElapsed(start));
                throw;
            }
        }
    }
}
