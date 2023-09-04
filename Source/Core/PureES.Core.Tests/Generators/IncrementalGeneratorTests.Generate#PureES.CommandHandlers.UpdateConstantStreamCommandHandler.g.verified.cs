﻿//HintName: PureES.CommandHandlers.UpdateConstantStreamCommandHandler.g.cs
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
    internal class UpdateConstantStreamCommandHandler : global::PureES.Core.ICommandHandler<global::PureES.Core.Tests.Models.Commands.UpdateConstantStream>
    {
        private readonly global::PureES.Core.PureESStreamId<global::PureES.Core.Tests.Models.Commands.UpdateConstantStream> _getStreamId;
        private readonly global::PureES.Core.IAggregateStore<global::PureES.Core.Tests.Models.TestAggregates.Aggregate> _aggregateStore;
        private readonly global::PureES.Core.EventStore.IEventStore _eventStore;
        private readonly global::System.Collections.Generic.IEnumerable<global::PureES.Core.IEventEnricher> _enrichers;
        private readonly global::System.Collections.Generic.IEnumerable<global::PureES.Core.ICommandValidator<global::PureES.Core.Tests.Models.Commands.UpdateConstantStream>> _syncValidators;
        private readonly global::System.Collections.Generic.IEnumerable<global::PureES.Core.IAsyncCommandValidator<global::PureES.Core.Tests.Models.Commands.UpdateConstantStream>> _asyncValidators;
        private readonly global::Microsoft.Extensions.Logging.ILogger<UpdateConstantStreamCommandHandler> _logger;
        private readonly global::System.IServiceProvider _service0;

        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public UpdateConstantStreamCommandHandler(
            global::System.IServiceProvider service0,
            global::PureES.Core.PureESStreamId<global::PureES.Core.Tests.Models.Commands.UpdateConstantStream> getStreamId,
            global::PureES.Core.IOptimisticConcurrency optimisticConcurrency,
            global::PureES.Core.EventStore.IEventStore eventStore,
            global::PureES.Core.IAggregateStore<global::PureES.Core.Tests.Models.TestAggregates.Aggregate> aggregateStore,
            global::System.Collections.Generic.IEnumerable<global::PureES.Core.IEventEnricher> enrichers = null,
            global::System.Collections.Generic.IEnumerable<global::PureES.Core.IEventEnricher> enrichers = null,
            global::System.Collections.Generic.IEnumerable<global::PureES.Core.ICommandValidator<global::PureES.Core.Tests.Models.Commands.UpdateConstantStream>> syncValidators = null,
            global::System.Collections.Generic.IEnumerable<global::PureES.Core.IAsyncCommandValidator<global::PureES.Core.Tests.Models.Commands.UpdateConstantStream>> asyncValidators = null,
            global::Microsoft.Extensions.Logging.ILogger<UpdateConstantStreamCommandHandler> logger = null)
        {
            this._getStreamId = getStreamId ?? throw new ArgumentNullException(nameof(getStreamId))
            this._eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
            this._aggregateStore = aggregateStore ?? throw new ArgumentNullException(nameof(aggregateStore));
            this._concurrency = concurrency;
            this._enrichers = enrichers;
            this._syncValidators = syncValidators;
            this._asyncValidators = asyncValidators;
            this._logger = logger;
            this._service0 = service0 ?? throw new ArgumentNullException(nameof(service0));
        }



        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        private static double GetElapsed(long start)
        {
            return (global::System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000 / (double)Stopwatch.Frequency;
        }


        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public async global::System.Threading.Tasks.Task<global::PureES.Core.Tests.Models.Commands.UpdateConstantStream> Handle(global::PureES.Core.Tests.Models.Commands.UpdateConstantStream command, CancellationToken cancellationToken)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }
            this._logger?.Log(
                logLevel: LogEventLevel.Debug,
                exception: null,
                message: "Handling command {@Command}. Aggregate: {@Aggregate}. Method: {@Method}",
                typeof(global::PureES.Core.Tests.Models.TestAggregates.Aggregate),
                typeof(global::PureES.Core.Tests.Models.Commands.UpdateConstantStream),
                "UpdateOnResult");
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
                const string streamId = "UpdateConstantStream";
                var currentRevision = this._concurrency?.GetExpectedRevision(streamId, command, cancellationToken) ?? await this._eventStore.GetRevision(streamId, cancellationToken);
                var current = await _aggregateStore.Load(streamId, currentRevision, cancellationToken);
                var result = current.UpdateOnResult(command, this._service0);
                var revision = currentRevision;
                if (result?.Event != null)
                {
                    var e = new global::PureES.Core.UncommittedEvent() { Event = result.Event });
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
                    logLevel: LogEventLevel.Information,
                    exception: null,
                    message: "Handled command {@Command}. Elapsed: {0.0000}ms. Stream {StreamId} is now at {Revision}. Aggregate: {@Aggregate}. Method: {@Method}",
                    typeof(global::PureES.Core.Tests.Models.TestAggregates.Aggregate),
                    GetElapsed(start),
                    streamId,
                    revision,
                    typeof(global::PureES.Core.Tests.Models.Commands.UpdateConstantStream),
                    "UpdateOnResult");
                return result?.Result;
            }
            catch (global::System.Exception ex)
            {
                this._logger?.Log(
                    logLevel: LogEventLevel.Information,
                    exception: ex,
                    message: "Error handling command {@Command}. Aggregate: {@Aggregate}. Method: {@Method}. Elapsed: {0.0000}ms",
                    typeof(global::PureES.Core.Tests.Models.TestAggregates.Aggregate),
                    typeof(global::PureES.Core.Tests.Models.Commands.UpdateConstantStream),
                    "UpdateOnResult",
                    GetElapsed(start));
                throw;
            }
        }
    }
}
