﻿//HintName: PureES.EventHandlers.Events_DerivedUpdatedEventHandler_PureESTestsModelsTestEventHandlers_OnDerivedUpdated.g.cs
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
    ///<summary><c>PureES.Tests.Models.TestEventHandlers.OnDerivedUpdated</c></summary>
    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("PureES.SourceGenerator", "1.0.0.0")]
    internal class Events_DerivedUpdatedEventHandler_PureESTestsModelsTestEventHandlers_OnDerivedUpdated : global::PureES.IEventHandler<PureES.Tests.Models.Events.DerivedUpdated>
    {
        private readonly global::Microsoft.Extensions.Logging.ILogger<Events_DerivedUpdatedEventHandler_PureESTestsModelsTestEventHandlers_OnDerivedUpdated> _logger;
        private readonly global::PureES.PureESEventHandlerOptions _options;
        private readonly global::PureES.Tests.Models.TestEventHandlers _parent;

        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public Events_DerivedUpdatedEventHandler_PureESTestsModelsTestEventHandlers_OnDerivedUpdated(
            global::PureES.Tests.Models.TestEventHandlers parent,
            global::Microsoft.Extensions.Options.IOptions<PureES.PureESOptions> options,
            global::Microsoft.Extensions.Logging.ILogger<Events_DerivedUpdatedEventHandler_PureESTestsModelsTestEventHandlers_OnDerivedUpdated> logger = null)
        {
            this._options = options?.Value.EventHandlers ?? throw new ArgumentNullException(nameof(options));
            this._logger = logger ?? global::Microsoft.Extensions.Logging.Abstractions.NullLogger<Events_DerivedUpdatedEventHandler_PureESTestsModelsTestEventHandlers_OnDerivedUpdated>.Instance;
            this._parent = parent ?? throw new ArgumentNullException(nameof(parent));
        }
        private static readonly global::System.Type ParentType = typeof(global::PureES.Tests.Models.TestEventHandlers);
        private static readonly global::System.Type EventType = typeof(global::PureES.Tests.Models.Events.DerivedUpdated);
        private static readonly global::System.Reflection.MethodInfo _method = ParentType.GetMethod(name: "OnDerivedUpdated", bindingAttr: global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.Instance, types: new [] { typeof(global::PureES.Tests.Models.EventEnvelope<global::PureES.Tests.Models.Events.DerivedUpdated>) });

        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        public global::System.Reflection.MethodInfo Method
        {

            [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
            [global::System.Diagnostics.DebuggerStepThroughAttribute()]
            [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
            get => _method ?? throw new InvalidOperationException($"Could not locate method 'OnDerivedUpdated' on {ParentType}");
        }

        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        public int Priority
        {

            [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
            [global::System.Diagnostics.DebuggerStepThroughAttribute()]
            [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
            get => 20;
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
        public Task Handle(global::PureES.EventEnvelope @event)
        {
            if (@event.Event is not global::PureES.Tests.Models.Events.DerivedUpdated)
            {
                throw new ArgumentOutOfRangeException($"Unknown event type {@event.Event.GetType()}", nameof(@event));
            }
            using (var activity = PureES.PureESTracing.ActivitySource.StartActivity("HandleEvent"))
            {
                if (activity != null)
                {
                    activity.DisplayName = "HandleEvent TestEventHandlers.OnDerivedUpdated (Events+DerivedUpdated)";
                    if (activity.IsAllDataRequested)
                    {
                        activity?.SetTag("StreamId", @event.StreamId);
                        activity?.SetTag("StreamPosition", @event.StreamPosition);
                        activity?.SetTag("HandlerClass", "PureES.Tests.Models.TestEventHandlers");
                        activity?.SetTag("HandlerMethod", "OnDerivedUpdated");
                        activity?.SetTag("HandlerEventType", "PureES.Tests.Models.Events.DerivedUpdated");
                        activity?.SetTag("EventType", global::PureES.BasicEventTypeMap.GetTypeName(@event.Event.GetType()));
                    }
                }
                using (_logger.BeginScope(new global::System.Collections.Generic.Dictionary<string, object>()
                    {
                        { "StreamId", @event.StreamId },
                        { "StreamPosition", @event.StreamPosition },
                        { "HandlerClass", "PureES.Tests.Models.TestEventHandlers" },
                        { "HandlerMethod", "OnDerivedUpdated" },
                        { "HandlerEventType", "PureES.Tests.Models.Events.DerivedUpdated" },
                        { "EventType", global::PureES.BasicEventTypeMap.GetTypeName(@event.Event.GetType()) },
                    }))
                {
                    var ct = new CancellationTokenSource(_options.Timeout).Token;
                    var start = global::System.Diagnostics.Stopwatch.GetTimestamp();
                    try
                    {
                        this._logger.Log(
                            logLevel: global::Microsoft.Extensions.Logging.LogLevel.Debug,
                            exception: null,
                            message: "Handling event {StreamId}/{StreamPosition}. Event Type: {@EventType}. Event handler {EventHandler} on {@EventHandlerParent}",
                            @event.StreamId,
                            @event.StreamPosition,
                            @event.Event.GetType(),
                            "OnDerivedUpdated",
                            ParentType);
                        this._parent.OnDerivedUpdated(
                            new global::PureES.Tests.Models.EventEnvelope<global::PureES.Tests.Models.Events.DerivedUpdated>(@event));
                        var elapsed = GetElapsedTimespan(start);
                        this._logger.Log(
                            logLevel: this._options.GetLogLevel(@event, elapsed),
                            exception: null,
                            message: "Handled event {StreamId}/{StreamPosition}. Elapsed: {Elapsed:0.0000}ms. Event Type: {@EventType}. Event handler {EventHandler} on {@EventHandlerParent}",
                            @event.StreamId,
                            @event.StreamPosition,
                            elapsed.TotalMilliseconds,
                            @event.Event.GetType(),
                            "OnDerivedUpdated",
                            ParentType);
                    }
                    catch (global::System.Exception ex)
                    {
                        this._logger.Log(
                            logLevel: _options.PropagateExceptions ? LogLevel.Information : LogLevel.Error,
                            exception: ex,
                            message: "Error handling event {StreamId}/{StreamPosition}. Elapsed: {Elapsed:0.0000}ms. Event Type: {@EventType}. Event handler {EventHandler} on {@EventHandlerParent}",
                            @event.StreamId,
                            @event.StreamPosition,
                            GetElapsed(start),
                            @event.Event.GetType(),
                            "OnDerivedUpdated",
                            ParentType);
                        if (activity != null)
                        {
                            activity.SetStatus(global::System.Diagnostics.ActivityStatusCode.Error, ex.Message);
                            activity.SetTag("error.type", ex.GetType().FullName);
                            activity.AddException(ex);
                        }
                        if (_options.PropagateExceptions)
                        {
                            throw;
                        }
                    }
                    return Task.CompletedTask;
                }
            }
        }
    }
}
