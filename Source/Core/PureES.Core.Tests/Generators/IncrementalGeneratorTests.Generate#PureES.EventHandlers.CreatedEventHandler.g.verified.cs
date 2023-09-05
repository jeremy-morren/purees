﻿//HintName: PureES.EventHandlers.CreatedEventHandler.g.cs
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
    internal class CreatedEventHandler : global::PureES.Core.IEventHandler<global::PureES.Core.Tests.Models.Events.Created>
    {
        private readonly global::Microsoft.Extensions.Logging.ILogger<CreatedEventHandler> _logger;
        private readonly global::PureES.Core.PureESEventHandlerOptions _options;
        private readonly global::Microsoft.Extensions.Logging.ILoggerFactory _service0;
        private readonly global::PureES.Core.Tests.Models.TestAggregates.EventHandlers _parent0;

        public CreatedEventHandler(
            global::Microsoft.Extensions.Logging.ILoggerFactory service0,
            global::PureES.Core.Tests.Models.TestAggregates.EventHandlers parent0,
            global::Microsoft.Extensions.Options.IOptions<global::PureES.Core.PureESOptions> options,
            global::Microsoft.Extensions.Logging.ILogger<CreatedEventHandler> logger = null)
        {
            this._options = options?.Value.EventHandlers ?? throw new ArgumentNullException(nameof(options));
            this._logger = logger ?? global::Microsoft.Extensions.Logging.Abstractions.NullLogger<CreatedEventHandler>.Instance;
            this._service0 = service0 ?? throw new ArgumentNullException(nameof(service0));
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
        public async global::System.Threading.Tasks.Task Handle(global::PureES.Core.EventEnvelope @event)
        {
            var ct = new CancellationTokenSource(_options.Timeout).Token;
            using (var activity = new global::System.Diagnostics.Activity("PureES.EventHandlers.EventHandler"))
            {
                activity.SetTag("StreamId", @event.StreamId);
                activity.SetTag("StreamPosition", @event.StreamPosition);
                activity.SetTag("EventType", "PureES.Core.Tests.Models.Events.Created");
                global::System.Diagnostics.Activity.Current = activity;
                activity.Start();
                var tasks = new global::System.Threading.Tasks.Task[2];
                // OnCreated on PureES.Core.Tests.Models.TestAggregates.EventHandlers
                tasks[0] = global::System.Threading.Tasks.Task.Run(async () =>
                {
                    using (_logger.BeginScope(new global::System.Collections.Generic.Dictionary<string, object>()
                        {
                            { "EventType", typeof(global::PureES.Core.Tests.Models.Events.Created) },
                            { "EventHandlerParent", typeof(PureES.Core.Tests.Models.TestAggregates.EventHandlers) },
                            { "EventHandler", "OnCreated" },
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
                                message: "Handling event {@StreamId}/{@StreamPosition}. Event Type: {@EventType}. Event handler {@EventHandler} on {@EventHandlerParent}",
                                @event.StreamId,
                                @event.StreamPosition,
                                typeof(global::PureES.Core.Tests.Models.Events.Created),
                                "OnCreated",
                                typeof(global::PureES.Core.Tests.Models.TestAggregates.EventHandlers));
                            await global::PureES.Core.Tests.Models.TestAggregates.EventHandlers.OnCreated(
                                (global::PureES.Core.Tests.Models.Events.Created)@event.Event,
                                ct);
                            var elapsed = GetElapsedTimespan(start);
                            this._logger?.Log(
                                logLevel: this._options.GetLogLevel(@event, elapsed),
                                exception: null,
                                message: "Handled event {@StreamId}/{@StreamPosition}. Elapsed: {0.0000}ms. Event Type: {@EventType}. Event handler {@EventHandler} on {@EventHandlerParent}",
                                @event.StreamId,
                                @event.StreamPosition,
                                elapsed.TotalMilliseconds,
                                typeof(global::PureES.Core.Tests.Models.Events.Created),
                                "OnCreated",
                                typeof(global::PureES.Core.Tests.Models.TestAggregates.EventHandlers));
                        }
                        catch (global::System.OperationCanceledException ex)
                        {
                            this._logger?.Log(
                                logLevel: _options.PropagateExceptions ? LogLevel.Information : LogLevel.Error,
                                exception: ex,
                                message: "Timed out while handling event {@StreamId}/{@StreamPosition}. Elapsed: {0.0000}ms. Event Type: {@EventType}. Event handler {@EventHandler} on {@EventHandlerParent}",
                                @event.StreamId,
                                @event.StreamPosition,
                                GetElapsed(start),
                                typeof(global::PureES.Core.Tests.Models.Events.Created),
                                "OnCreated",
                                typeof(global::PureES.Core.Tests.Models.TestAggregates.EventHandlers));
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
                                message: "Error handling event {@StreamId}/{@StreamPosition}. Elapsed: {0.0000}ms. Event Type: {@EventType}. Event handler {@EventHandler} on {@EventHandlerParent}",
                                @event.StreamId,
                                @event.StreamPosition,
                                GetElapsed(start),
                                typeof(global::PureES.Core.Tests.Models.Events.Created),
                                "OnCreated",
                                typeof(global::PureES.Core.Tests.Models.TestAggregates.EventHandlers));
                            if (_options.PropagateExceptions)
                            {
                                throw;
                            }
                        }
                    }
                });

                // OnCreated2 on PureES.Core.Tests.Models.TestAggregates.EventHandlers
                tasks[1] = global::System.Threading.Tasks.Task.Run(() =>
                {
                    using (_logger.BeginScope(new global::System.Collections.Generic.Dictionary<string, object>()
                        {
                            { "EventType", typeof(global::PureES.Core.Tests.Models.Events.Created) },
                            { "EventHandlerParent", typeof(PureES.Core.Tests.Models.TestAggregates.EventHandlers) },
                            { "EventHandler", "OnCreated2" },
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
                                message: "Handling event {@StreamId}/{@StreamPosition}. Event Type: {@EventType}. Event handler {@EventHandler} on {@EventHandlerParent}",
                                @event.StreamId,
                                @event.StreamPosition,
                                typeof(global::PureES.Core.Tests.Models.Events.Created),
                                "OnCreated2",
                                typeof(global::PureES.Core.Tests.Models.TestAggregates.EventHandlers));
                            this._parent0.OnCreated2(
                                new global::PureES.Core.EventEnvelope<global::PureES.Core.Tests.Models.Events.Created, object>(@event),
                                this._service0);
                            var elapsed = GetElapsedTimespan(start);
                            this._logger?.Log(
                                logLevel: this._options.GetLogLevel(@event, elapsed),
                                exception: null,
                                message: "Handled event {@StreamId}/{@StreamPosition}. Elapsed: {0.0000}ms. Event Type: {@EventType}. Event handler {@EventHandler} on {@EventHandlerParent}",
                                @event.StreamId,
                                @event.StreamPosition,
                                elapsed.TotalMilliseconds,
                                typeof(global::PureES.Core.Tests.Models.Events.Created),
                                "OnCreated2",
                                typeof(global::PureES.Core.Tests.Models.TestAggregates.EventHandlers));
                        }
                        catch (global::System.OperationCanceledException ex)
                        {
                            this._logger?.Log(
                                logLevel: _options.PropagateExceptions ? LogLevel.Information : LogLevel.Error,
                                exception: ex,
                                message: "Timed out while handling event {@StreamId}/{@StreamPosition}. Elapsed: {0.0000}ms. Event Type: {@EventType}. Event handler {@EventHandler} on {@EventHandlerParent}",
                                @event.StreamId,
                                @event.StreamPosition,
                                GetElapsed(start),
                                typeof(global::PureES.Core.Tests.Models.Events.Created),
                                "OnCreated2",
                                typeof(global::PureES.Core.Tests.Models.TestAggregates.EventHandlers));
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
                                message: "Error handling event {@StreamId}/{@StreamPosition}. Elapsed: {0.0000}ms. Event Type: {@EventType}. Event handler {@EventHandler} on {@EventHandlerParent}",
                                @event.StreamId,
                                @event.StreamPosition,
                                GetElapsed(start),
                                typeof(global::PureES.Core.Tests.Models.Events.Created),
                                "OnCreated2",
                                typeof(global::PureES.Core.Tests.Models.TestAggregates.EventHandlers));
                            if (_options.PropagateExceptions)
                            {
                                throw;
                            }
                        }
                    }
                });

                await global::System.Threading.Tasks.Task.WhenAll(tasks);
            }
        }
    }
}
