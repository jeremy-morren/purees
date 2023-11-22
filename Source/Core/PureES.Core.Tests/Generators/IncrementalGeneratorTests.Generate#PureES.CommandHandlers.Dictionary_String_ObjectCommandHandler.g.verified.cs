﻿//HintName: PureES.CommandHandlers.Dictionary_String_ObjectCommandHandler.g.cs
// <auto-generated/>

// This file was automatically generated by the PureES source generator.
// Do not edit this file manually since it will be automatically overwritten.
// ReSharper disable All

#nullable disable
#pragma warning disable CS0162 //Unreachable code detected

#pragma warning disable CS8019 //Unnecessary using directive
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;


namespace PureES.CommandHandlers
{
    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("PureES.SourceGenerator", "1.0.0.0")]
    internal class Dictionary_String_ObjectCommandHandler : global::PureES.Core.ICommandHandler<global::System.Collections.Generic.Dictionary<string, object>>
    {
        private readonly global::PureES.Core.ICommandStreamId<global::System.Collections.Generic.Dictionary<string, object>> _getStreamId;
        private readonly global::PureES.Core.IAggregateStore<global::PureES.Core.Tests.Models.ImplementedGenericAggregate> _aggregateStore;
        private readonly global::PureES.Core.IEventStore _eventStore;
        private readonly global::PureES.Core.IOptimisticConcurrency _concurrency;
        private readonly global::System.Collections.Generic.IEnumerable<global::PureES.Core.IEventEnricher> _enrichers;
        private readonly global::System.Collections.Generic.IEnumerable<global::PureES.Core.IAsyncEventEnricher> _asyncEnrichers;
        private readonly global::System.Collections.Generic.IEnumerable<global::PureES.Core.ICommandValidator<global::System.Collections.Generic.Dictionary<string, object>>> _syncValidators;
        private readonly global::System.Collections.Generic.IEnumerable<global::PureES.Core.IAsyncCommandValidator<global::System.Collections.Generic.Dictionary<string, object>>> _asyncValidators;
        private readonly global::Microsoft.Extensions.Logging.ILogger<Dictionary_String_ObjectCommandHandler> _logger;

        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public Dictionary_String_ObjectCommandHandler(
            global::PureES.Core.ICommandStreamId<global::System.Collections.Generic.Dictionary<string, object>> getStreamId,
            global::PureES.Core.IEventStore eventStore,
            global::PureES.Core.IAggregateStore<global::PureES.Core.Tests.Models.ImplementedGenericAggregate> aggregateStore,
            global::PureES.Core.IOptimisticConcurrency concurrency = null,
            global::System.Collections.Generic.IEnumerable<global::PureES.Core.IEventEnricher> enrichers = null,
            global::System.Collections.Generic.IEnumerable<global::PureES.Core.IAsyncEventEnricher> asyncEnrichers = null,
            global::System.Collections.Generic.IEnumerable<global::PureES.Core.ICommandValidator<global::System.Collections.Generic.Dictionary<string, object>>> syncValidators = null,
            global::System.Collections.Generic.IEnumerable<global::PureES.Core.IAsyncCommandValidator<global::System.Collections.Generic.Dictionary<string, object>>> asyncValidators = null,
            global::Microsoft.Extensions.Logging.ILogger<Dictionary_String_ObjectCommandHandler> logger = null)
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
        }



        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static double GetElapsed(long start)
        {
#if NET7_0_OR_GREATER
            return global::System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
#else
            return (global::System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000 / (double)global::System.Diagnostics.Stopwatch.Frequency;
#endif
        }


        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public async global::System.Threading.Tasks.Task<ulong> Handle(global::System.Collections.Generic.Dictionary<string, object> command, CancellationToken cancellationToken)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }
            var commandType = typeof(global::System.Collections.Generic.Dictionary<string, object>);
            var aggregateType = typeof(global::PureES.Core.Tests.Models.ImplementedGenericAggregate);
            this._logger?.Log(
                logLevel: global::Microsoft.Extensions.Logging.LogLevel.Debug,
                exception: null,
                message: "Handling command {@Command}. Aggregate: {@Aggregate}. Method: {Method}",
                commandType,
                aggregateType,
                "Update");
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
                var streamId = this._getStreamId.GetStreamId(command);
                var currentRevision = this._concurrency?.GetExpectedRevision(streamId, command) ?? await this._eventStore.GetRevision(streamId, cancellationToken);
                var current = await _aggregateStore.Load(streamId, currentRevision, cancellationToken);
                var result = current.Update(command);
                var revision = currentRevision;
                if (result != null)
                {
                    var e = new global::PureES.Core.UncommittedEvent(result);
                    if (this._enrichers != null)
                    {
                        foreach (var enricher in this._enrichers)
                        {
                            enricher.Enrich(e);
                        }
                    }
                    if (this._asyncEnrichers != null)
                    {
                        foreach (var enricher in this._asyncEnrichers)
                        {
                            await enricher.Enrich(e, cancellationToken);
                        }
                    }
                    revision = await _eventStore.Append(streamId, currentRevision, e, cancellationToken);
                }
                this._concurrency?.OnUpdated(streamId, command, currentRevision, revision);
                this._logger?.Log(
                    logLevel: global::Microsoft.Extensions.Logging.LogLevel.Information,
                    exception: null,
                    message: "Handled command {@Command}. Elapsed: {Elapsed:0.0000}ms. Stream {StreamId} is now at {Revision}. Aggregate: {@Aggregate}. Method: {Method}",
                    commandType,
                    GetElapsed(start),
                    streamId,
                    revision,
                    aggregateType,
                    "Update");
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
                    "Update",
                    GetElapsed(start));
                throw;
            }
        }
    }
}
