﻿//HintName: PureES.EventHandlers.UpdatedEventHandler.g.cs
// <auto-generated/>
// This file was automatically generated by the PureES source generator.
// Do not edit this file manually since it will be automatically overwritten.
// ReSharper disable All

#nullable disable

using System;
using System;
using Microsoft.Extensions.Logging;

namespace PureES.EventHandlers
{
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("PureES.SourceGenerator", "1.0.0.0")]
    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
    internal class UpdatedEventHandler : global::PureES.Core.IEventHandler<global::PureES.Core.Tests.Models.Events.Updated>
    {
        private readonly global::Microsoft.Extensions.Logging.ILogger<UpdatedEventHandler> _logger;
        private readonly global::PureES.Core.PureESEventHandlerOptions _options;
        private readonly global::PureES.Core.Tests.Models.TestAggregates.EventHandlers _parent0;

        public UpdatedEventHandler(
            global::PureES.Core.Tests.Models.TestAggregates.EventHandlers parent0,
            global::Microsoft.Extensions.Options.IOptions<global::PureES.Core.PureESOptions> options,
            global::Microsoft.Extensions.Logging.ILogger<UpdatedEventHandler> logger = null)
        {
            this._options = options?.Value.EventHandlers ?? throw new ArgumentNullException(nameof(options));
            this._logger = logger ?? global::Microsoft.Extensions.Logging.Abstractions.NullLogger<UpdatedEventHandler>.Instance;
            this._parent0 = parent0 ?? throw new ArgumentNullException(nameof(parent0));
        }

        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        private static double GetElapsed(long start)
        {
            return (global::System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000 / (double)global::System.Diagnostics.Stopwatch.Frequency;
        }

        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        private static TimeSpan GetElapsedTimespan(long start)
        {
            return global::System.TimeSpan.FromSeconds((global::System.Diagnostics.Stopwatch.GetTimestamp() - start) / (double)global::System.Diagnostics.Stopwatch.Frequency);
        }

        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public global::System.Threading.Tasks.Task Handle(global::PureES.Core.EventEnvelope @event)
        {
            if (@event.Event is not global::PureES.Core.Tests.Models.Events.Updated)
            {
                throw new ArgumentException(nameof(@event));
            }
            using (var activity = new global::System.Diagnostics.Activity("PureES.EventHandlers.EventHandler"))
            {
                activity.SetTag("StreamId", @event.StreamId);
                activity.SetTag("StreamPosition", @event.StreamPosition);
                activity.SetTag("EventType", "PureES.Core.Tests.Models.Events.Updated");
                global::System.Diagnostics.Activity.Current = activity;
                activity.Start();
                PureESCoreTestsModelsTestAggregatesEventHandlers_OnUpdated(@event);
                return global::System.Threading.Tasks.Task.CompletedTask;
            }
        }

        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        // OnUpdated on PureES.Core.Tests.Models.TestAggregates.EventHandlers
        private void PureESCoreTestsModelsTestAggregatesEventHandlers_OnUpdated(global::PureES.Core.EventEnvelope @event)
        {
            var ct = new CancellationTokenSource(_options.Timeout).Token;
            var parentType = typeof(global::PureES.Core.Tests.Models.TestAggregates.EventHandlers);
            var eventType = typeof(global::PureES.Core.Tests.Models.Events.Updated);
            using (_logger.BeginScope(new global::System.Collections.Generic.Dictionary<string, object>()
                {
                    { "EventType", eventType },
                    { "EventHandlerParent", parentType },
                    { "EventHandler", "OnUpdated" },
                    { "StreamId", @event.StreamId },
                    { "StreamPosition", @event.StreamPosition },
                }))
            {
                var start = global::System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    this._logger?.Log(
                        logLevel: global::Microsoft.Extensions.Logging.LogLevel.Debug,
                        exception: null,
                        message: "Handling event {StreamId}/{StreamPosition}. Event Type: {@EventType}. Event handler {EventHandler} on {@EventHandlerParent}",
                        @event.StreamId,
                        @event.StreamPosition,
                        eventType,
                        "OnUpdated",
                        parentType);
                    this._parent0.OnUpdated(
                        new global::PureES.Core.Tests.Models.TestAggregates.EventEnvelope<global::PureES.Core.Tests.Models.Events.Updated>(@event));
                    var elapsed = GetElapsedTimespan(start);
                    this._logger?.Log(
                        logLevel: this._options.GetLogLevel(@event, elapsed),
                        exception: null,
                        message: "Handled event {StreamId}/{StreamPosition}. Elapsed: {Elapsed:0.0000}ms. Event Type: {@EventType}. Event handler {EventHandler} on {@EventHandlerParent}",
                        @event.StreamId,
                        @event.StreamPosition,
                        elapsed.TotalMilliseconds,
                        eventType,
                        "OnUpdated",
                        parentType);
                }
                catch (global::System.OperationCanceledException ex)
                {
                    this._logger?.Log(
                        logLevel: _options.PropagateExceptions ? LogLevel.Information : LogLevel.Error,
                        exception: ex,
                        message: "Timed out while handling event {StreamId}/{StreamPosition}. Elapsed: {Elapsed:0.0000}ms. Event Type: {@EventType}. Event handler {EventHandler} on {@EventHandlerParent}",
                        @event.StreamId,
                        @event.StreamPosition,
                        GetElapsed(start),
                        eventType,
                        "OnUpdated",
                        parentType);
                    if (_options.PropagateExceptions)
                    {
                        throw;
                    }
                }
                catch (global::System.Exception ex)
                {
                    this._logger?.Log(
                        logLevel: _options.PropagateExceptions ? LogLevel.Information : LogLevel.Error,
                        exception: ex,
                        message: "Error handling event {StreamId}/{StreamPosition}. Elapsed: {Elapsed:0.0000}ms. Event Type: {@EventType}. Event handler {EventHandler} on {@EventHandlerParent}",
                        @event.StreamId,
                        @event.StreamPosition,
                        GetElapsed(start),
                        eventType,
                        "OnUpdated",
                        parentType);
                    if (_options.PropagateExceptions)
                    {
                        throw;
                    }
                }
            }
        }
    }
}
