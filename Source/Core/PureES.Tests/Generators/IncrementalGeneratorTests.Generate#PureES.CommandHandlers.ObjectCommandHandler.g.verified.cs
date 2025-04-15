﻿//HintName: PureES.CommandHandlers.objectCommandHandler.g.cs
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
    internal sealed class objectCommandHandler : global::PureES.ICommandHandler<object>
    {
        private readonly PureES.ICommandStreamId<object> _getStreamId;
        private readonly PureES.IAggregateStore<global::PureES.Tests.Models.ImplementedGenericAggregate> _aggregateStore;
        private readonly global::PureES.IEventStore _eventStore;
        private readonly global::PureES.IConcurrency _concurrency;
        private readonly global::System.Collections.Generic.IEnumerable<global::PureES.IEventEnricher> _syncEnrichers;
        private readonly global::System.Collections.Generic.IEnumerable<global::PureES.IAsyncEventEnricher> _asyncEnrichers;
        private readonly global::System.Collections.Generic.IEnumerable<global::PureES.ICommandValidator<object>> _syncValidators;
        private readonly global::System.Collections.Generic.IEnumerable<global::PureES.IAsyncCommandValidator<object>> _asyncValidators;
        private readonly global::Microsoft.Extensions.Logging.ILogger<objectCommandHandler> _logger;

        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public objectCommandHandler(
            PureES.ICommandStreamId<object> getStreamId,
            global::PureES.IEventStore eventStore,
            PureES.IAggregateStore<global::PureES.Tests.Models.ImplementedGenericAggregate> aggregateStore,
            global::System.Collections.Generic.IEnumerable<global::PureES.IEventEnricher> syncEnrichers,
            global::System.Collections.Generic.IEnumerable<global::PureES.IAsyncEventEnricher> asyncEnrichers,
            global::System.Collections.Generic.IEnumerable<global::PureES.ICommandValidator<object>> syncValidators,
            global::System.Collections.Generic.IEnumerable<global::PureES.IAsyncCommandValidator<object>> asyncValidators,
            global::PureES.IConcurrency concurrency = null,
            global::Microsoft.Extensions.Logging.ILogger<objectCommandHandler> logger = null)
        {
            this._getStreamId = getStreamId ?? throw new ArgumentNullException(nameof(getStreamId));
            this._eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
            this._aggregateStore = aggregateStore ?? throw new ArgumentNullException(nameof(aggregateStore));
            this._syncEnrichers = syncEnrichers ?? throw new ArgumentNullException(nameof(syncEnrichers));
            this._asyncEnrichers = asyncEnrichers ?? throw new ArgumentNullException(nameof(asyncEnrichers));
            this._syncValidators = syncValidators ?? throw new ArgumentNullException(nameof(syncValidators));
            this._asyncValidators = asyncValidators ?? throw new ArgumentNullException(nameof(asyncValidators));
            this._concurrency = concurrency;
            this._logger = logger ?? global::Microsoft.Extensions.Logging.Abstractions.NullLogger<objectCommandHandler>.Instance;
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

        private static readonly global::System.Type AggregateType = typeof(global::PureES.Tests.Models.ImplementedGenericAggregate);
        private static readonly global::System.Type CommandType = typeof(object);

        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public async global::System.Threading.Tasks.Task<uint> Handle(object command, CancellationToken cancellationToken)
        {
#if NET6_0_OR_GREATER
            global::System.ArgumentNullException.ThrowIfNull(command, nameof(command));
#else
            if (command is null) throw new global::System.ArgumentNullException(nameof(command));
#endif
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }
            this._logger.Log(
                logLevel: global::Microsoft.Extensions.Logging.LogLevel.Debug,
                exception: null,
                message: "Handling command {@Command}. Aggregate: {@Aggregate}. Method: {Method}",
                CommandType,
                AggregateType,
                "Create");
            using (var activity = PureES.PureESTracing.ActivitySource.StartActivity("HandleCommand"))
            {
                if (activity != null)
                {
                    activity.DisplayName = "HandleCommand ImplementedGenericAggregate.Create";
                    if (activity.IsAllDataRequested)
                    {
                        activity?.SetTag("Command", CommandType);
                        activity?.SetTag("Aggregate", AggregateType);
                        activity?.SetTag("Method", "Create");
                    }
                }
                using (_logger.BeginScope(new global::System.Collections.Generic.Dictionary<string, object>()
                    {
                        { "Command", CommandType },
                        { "Aggregate", AggregateType },
                        { "Method", "Create" },
                    }))
                {
                    var start = global::System.Diagnostics.Stopwatch.GetTimestamp();
                    try
                    {
                        foreach (var v in this._syncValidators)
                        {
                            v.Validate(command);
                        }
                        foreach (var v in this._asyncValidators)
                        {
                            await v.Validate(command, cancellationToken);
                        }
                        var streamId = this._getStreamId.GetStreamId(command);
                        var result = global::PureES.Tests.Models.ImplementedGenericAggregate.Create(command);
                        var revision = uint.MaxValue;
                        if (result != null)
                        {
                            var e = new global::PureES.UncommittedEvent(result);
                            foreach (var enricher in this._syncEnrichers)
                            {
                                enricher.Enrich(e);
                            }
                            foreach (var enricher in this._asyncEnrichers)
                            {
                                await enricher.Enrich(e, cancellationToken);
                            }
                            revision = await _eventStore.Create(streamId, e, cancellationToken);
                        }
                        this._concurrency?.OnUpdated(streamId, command, null, revision);
                        this._logger.Log(
                            logLevel: global::Microsoft.Extensions.Logging.LogLevel.Information,
                            exception: null,
                            message: "Handled command {@Command}. Elapsed: {Elapsed:0.0000}ms. Stream {StreamId} is now at {Revision}. Aggregate: {@Aggregate}. Method: {Method}",
                            CommandType,
                            GetElapsed(start),
                            streamId,
                            revision,
                            AggregateType,
                            "Create");
                        return revision;
                        activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Ok);
                    }
                    catch (global::System.Exception ex)
                    {
                        this._logger.Log(
                            logLevel: global::Microsoft.Extensions.Logging.LogLevel.Information,
                            exception: ex,
                            message: "Error handling command {@Command}. Aggregate: {@Aggregate}. Method: {Method}. Elapsed: {Elapsed:0.0000}ms",
                            CommandType,
                            AggregateType,
                            "Create",
                            GetElapsed(start));
                        if (activity != null)
                        {
                            activity.SetStatus(global::System.Diagnostics.ActivityStatusCode.Error, ex.Message);
                            activity.SetTag("error.type", ex.GetType().FullName);
                            activity.AddException(ex);
                        }
                        throw;
                    }
                }
            }
        }
    }
}
