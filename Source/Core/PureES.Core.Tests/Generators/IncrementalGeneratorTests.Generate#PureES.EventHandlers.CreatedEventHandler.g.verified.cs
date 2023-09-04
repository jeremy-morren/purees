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
        private readonly global::Microsoft.Extensions.Options.IOptions<global::PureES.Core.PureESOptions> _options;
        private readonly global::Microsoft.Extensions.Logging.ILoggerFactory _service0;
        private readonly global::PureES.Core.Tests.Models.TestAggregates.EventHandlers _parent0;

        public CreatedEventHandler(
            global::Microsoft.Extensions.Logging.ILoggerFactory service0,
            global::PureES.Core.Tests.Models.TestAggregates.EventHandlers parent0,
            global::Microsoft.Extensions.Options.IOptions<global::PureES.Core.PureESOptions> options,
            global::Microsoft.Extensions.Logging.ILogger<CreatedEventHandler> logger = null)
        {
            this._options = options ?? throw new ArgumentNullException(nameof(options));
            this._logger = logger;
            this._service0 = service0 ?? throw new ArgumentNullException(nameof(service0));
            this._parent0 = parent0 ?? throw new ArgumentNullException(nameof(parent0));
        }

        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        private static double GetElapsed(long start)
        {
            return (global::System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000 / (double)Stopwatch.Frequency;
        }

        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public async System.Threading.Tasks.Task Handle(global::PureES.Core.EventEnvelope @event, CancellationToken cancellationToken)
        {
            ulong start;
            // OnCreated on PureES.Core.Tests.Models.TestAggregates.EventHandlers
            start = global::System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                this._logger?.Log(
                    logLevel: LogEventLevel.Debug,
                    exception: null,
                    message: "Handling event {@StreamId}/{@StreamPosition}. Event Type: {@EventType}. Event handler {@EventHandler} on {@EventHandlerParent}",
                    @event.StreamId,
                    @event.StreamPosition,
                    typeof(global::PureES.Core.Tests.Models.Events.Created),
                    "OnCreated",
                    typeof(global::PureES.Core.Tests.Models.TestAggregates.EventHandlers));
                await global::PureES.Core.Tests.Models.TestAggregates.EventHandlers.OnCreated(
                    (global::PureES.Core.Tests.Models.Events.Created)@event.Event,
                    cancellationToken);
                this._logger?.Log(
                    logLevel: LogEventLevel.Information,
                    exception: null,
                    message: "Handled event {@StreamId}/{@StreamPosition}. Elapsed: {0.0000}ms. Event Type: {@EventType}. Event handler {@EventHandler} on {@EventHandlerParent}",
                    @event.StreamId,
                    @event.StreamPosition,
                    GetElapsed(start),
                    typeof(global::PureES.Core.Tests.Models.Events.Created),
                    "OnCreated",
                    typeof(global::PureES.Core.Tests.Models.TestAggregates.EventHandlers));
            }
            catch (global::System.Exception ex)
            {
                this._logger?.Log(
                    logLevel: LogEventLevel.Error,
                    exception: ex,
                    message: "Error handling event {@StreamId}/{@StreamPosition}. Elapsed: {0.0000}ms. Event Type: {@EventType}. Event handler {@EventHandler} on {@EventHandlerParent}",
                    @event.StreamId,
                    @event.StreamPosition,
                    GetElapsed(start),
                    typeof(global::PureES.Core.Tests.Models.Events.Created),
                    "OnCreated",
                    typeof(global::PureES.Core.Tests.Models.TestAggregates.EventHandlers));
                if (_options.Value.PropagateEventHandlerExceptions)
                {
                    throw;
                }
            }

            // OnCreated2 on PureES.Core.Tests.Models.TestAggregates.EventHandlers
            start = global::System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                this._logger?.Log(
                    logLevel: LogEventLevel.Debug,
                    exception: null,
                    message: "Handling event {@StreamId}/{@StreamPosition}. Event Type: {@EventType}. Event handler {@EventHandler} on {@EventHandlerParent}",
                    @event.StreamId,
                    @event.StreamPosition,
                    typeof(global::PureES.Core.Tests.Models.Events.Created),
                    "OnCreated2",
                    typeof(global::PureES.Core.Tests.Models.TestAggregates.EventHandlers));
                parent0.OnCreated2(
                    new global::PureES.Core.EventEnvelope<global::PureES.Core.Tests.Models.Events.Created, object>(@event),
                    service0);
                this._logger?.Log(
                    logLevel: LogEventLevel.Information,
                    exception: null,
                    message: "Handled event {@StreamId}/{@StreamPosition}. Elapsed: {0.0000}ms. Event Type: {@EventType}. Event handler {@EventHandler} on {@EventHandlerParent}",
                    @event.StreamId,
                    @event.StreamPosition,
                    GetElapsed(start),
                    typeof(global::PureES.Core.Tests.Models.Events.Created),
                    "OnCreated2",
                    typeof(global::PureES.Core.Tests.Models.TestAggregates.EventHandlers));
            }
            catch (global::System.Exception ex)
            {
                this._logger?.Log(
                    logLevel: LogEventLevel.Error,
                    exception: ex,
                    message: "Error handling event {@StreamId}/{@StreamPosition}. Elapsed: {0.0000}ms. Event Type: {@EventType}. Event handler {@EventHandler} on {@EventHandlerParent}",
                    @event.StreamId,
                    @event.StreamPosition,
                    GetElapsed(start),
                    typeof(global::PureES.Core.Tests.Models.Events.Created),
                    "OnCreated2",
                    typeof(global::PureES.Core.Tests.Models.TestAggregates.EventHandlers));
                if (_options.Value.PropagateEventHandlerExceptions)
                {
                    throw;
                }
            }

        }
    }
}
