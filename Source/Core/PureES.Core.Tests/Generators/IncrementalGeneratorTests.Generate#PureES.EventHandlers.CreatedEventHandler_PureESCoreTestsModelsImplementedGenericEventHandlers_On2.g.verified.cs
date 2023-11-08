﻿//HintName: PureES.EventHandlers.CreatedEventHandler_PureESCoreTestsModelsImplementedGenericEventHandlers_On2.g.cs
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


namespace PureES.EventHandlers
{
    ///<summary><c>PureES.Core.Tests.Models.ImplementedGenericEventHandlers.On2</c></summary>
    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("PureES.SourceGenerator", "1.0.0.0")]
    internal class CreatedEventHandler_PureESCoreTestsModelsImplementedGenericEventHandlers_On2 : global::PureES.Core.IEventHandler<global::PureES.Core.Tests.Models.Events.Created>
    {
        private readonly global::Microsoft.Extensions.Logging.ILogger<CreatedEventHandler_PureESCoreTestsModelsImplementedGenericEventHandlers_On2> _logger;
        private readonly global::PureES.Core.PureESEventHandlerOptions _options;
        private readonly global::PureES.Core.Tests.Models.ImplementedGenericEventHandlers _parent;

        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public CreatedEventHandler_PureESCoreTestsModelsImplementedGenericEventHandlers_On2(
            global::PureES.Core.Tests.Models.ImplementedGenericEventHandlers parent,
            global::Microsoft.Extensions.Options.IOptions<global::PureES.Core.PureESOptions> options,
            global::Microsoft.Extensions.Logging.ILogger<CreatedEventHandler_PureESCoreTestsModelsImplementedGenericEventHandlers_On2> logger = null)
        {
            this._options = options?.Value.EventHandlers ?? throw new ArgumentNullException(nameof(options));
            this._logger = logger ?? global::Microsoft.Extensions.Logging.Abstractions.NullLogger<CreatedEventHandler_PureESCoreTestsModelsImplementedGenericEventHandlers_On2>.Instance;
            this._parent = parent ?? throw new ArgumentNullException(nameof(parent));
        }
        private static readonly global::System.Type ParentType = typeof(global::PureES.Core.Tests.Models.ImplementedGenericEventHandlers);
        private static readonly global::System.Type EventType = typeof(global::PureES.Core.Tests.Models.Events.Created);
        private static readonly global::System.Reflection.MethodInfo _method = ParentType.GetMethod(name: "On2", bindingAttr: global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.Static, types: new [] { typeof(global::PureES.Core.Tests.Models.Events.Created) });

        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        public global::System.Reflection.MethodInfo Method
        {

            [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
            [global::System.Diagnostics.DebuggerStepThroughAttribute()]
            [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
            get => _method ?? throw new InvalidOperationException($"Could not locate method 'On2' on {ParentType}");
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
        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static TimeSpan GetElapsedTimespan(long start)
        {
#if NET7_0_OR_GREATER
            return global::System.Diagnostics.Stopwatch.GetElapsedTime(start);
#else
            return global::System.TimeSpan.FromSeconds((global::System.Diagnostics.Stopwatch.GetTimestamp() - start) / (double)global::System.Diagnostics.Stopwatch.Frequency);
#endif
        }

        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public global::System.Threading.Tasks.Task Handle(global::PureES.Core.EventEnvelope @event)
        {
            if (@event.Event is not global::PureES.Core.Tests.Models.Events.Created)
            {
                throw new ArgumentException(nameof(@event));
            }
            using (var activity = new global::System.Diagnostics.Activity("PureES.EventHandlers.EventHandler"))
            {
                activity.SetTag("StreamId", @event.StreamId);
                activity.SetTag("StreamPosition", @event.StreamPosition);
                activity.SetTag("EventType", "PureES.Core.Tests.Models.Events.Created");
                global::System.Diagnostics.Activity.Current = activity;
                activity.Start();
                using (_logger.BeginScope(new global::System.Collections.Generic.Dictionary<string, object>()
                    {
                        { "EventType", EventType },
                        { "EventHandlerParent", ParentType },
                        { "EventHandler", "On2" },
                        { "StreamId", @event.StreamId },
                        { "StreamPosition", @event.StreamPosition },
                    }))
                {
                    var ct = new CancellationTokenSource(_options.Timeout).Token;
                    var start = global::System.Diagnostics.Stopwatch.GetTimestamp();
                    try
                    {
                        this._logger?.Log(
                            logLevel: global::Microsoft.Extensions.Logging.LogLevel.Debug,
                            exception: null,
                            message: "Handling event {StreamId}/{StreamPosition}. Event Type: {@EventType}. Event handler {EventHandler} on {@EventHandlerParent}",
                            @event.StreamId,
                            @event.StreamPosition,
                            EventType,
                            "On2",
                            ParentType);
                        global::PureES.Core.Tests.Models.ImplementedGenericEventHandlers.On2(
                            (global::PureES.Core.Tests.Models.Events.Created)@event.Event);
                        var elapsed = GetElapsedTimespan(start);
                        this._logger?.Log(
                            logLevel: this._options.GetLogLevel(@event, elapsed),
                            exception: null,
                            message: "Handled event {StreamId}/{StreamPosition}. Elapsed: {Elapsed:0.0000}ms. Event Type: {@EventType}. Event handler {EventHandler} on {@EventHandlerParent}",
                            @event.StreamId,
                            @event.StreamPosition,
                            elapsed.TotalMilliseconds,
                            EventType,
                            "On2",
                            ParentType);
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
                            EventType,
                            "On2",
                            ParentType);
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
                            EventType,
                            "On2",
                            ParentType);
                        if (_options.PropagateExceptions)
                        {
                            throw;
                        }
                    }
                    return global::System.Threading.Tasks.Task.CompletedTask;
                }
            }
        }
    }
}
