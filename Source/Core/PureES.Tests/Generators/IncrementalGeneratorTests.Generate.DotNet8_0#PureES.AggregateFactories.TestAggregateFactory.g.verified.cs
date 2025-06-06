﻿//HintName: PureES.AggregateFactories.TestAggregateFactory.g.cs
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


namespace PureES.AggregateFactories
{
    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("PureES.SourceGenerator", "1.0.0.0")]
    internal sealed class TestAggregateFactory : global::PureES.IAggregateFactory<global::PureES.Tests.Models.TestAggregate>
    {
        private readonly global::PureES.IEventStore _eventStore;
        private readonly global::System.IServiceProvider _services;

        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public TestAggregateFactory(
            global::PureES.IEventStore eventStore,
            global::System.IServiceProvider services)
        {
            this._eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
            this._services = services ?? throw new ArgumentNullException(nameof(services));
        }
        private static readonly global::System.Type AggregateType = typeof(global::PureES.Tests.Models.TestAggregate);

        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        private async Task<global::PureES.RehydratedAggregate<global::PureES.Tests.Models.TestAggregate>> CreateWhen(string streamId, global::System.Collections.Generic.IAsyncEnumerator<global::PureES.EventEnvelope> enumerator, TestAggregateFactory.Services services, CancellationToken ct)
        {
            if (!await enumerator.MoveNextAsync())
            {
                throw new ArgumentException("Stream is empty");
            }
            global::PureES.Tests.Models.TestAggregate current;
            switch (enumerator.Current.Event)
            {
                case global::PureES.Tests.Models.Events.Created e:
                {
                    try
                    {
                        current = global::PureES.Tests.Models.TestAggregate.When(
                            new global::PureES.EventEnvelope<global::PureES.Tests.Models.Events.Created, global::PureES.Tests.Models.Metadata>(enumerator.Current));
                    }
                    catch (Exception ex)
                    {
                        throw new global::PureES.RehydrationException(streamId, AggregateType, "PureES.Tests.Models.TestAggregate.When(PureES.EventEnvelope<PureES.Tests.Models.Events.Created, PureES.Tests.Models.Metadata>)", ex);
                    }
                    break;
                }
                default:
                {
                    var eventType = global::PureES.BasicEventTypeMap.GetTypeName(enumerator.Current.Event.GetType());
                    throw new global::PureES.RehydrationException(streamId, AggregateType, $"No suitable CreateWhen method found for event '{eventType}'");
                }
            }
            try
            {
                current.GlobalWhen(enumerator.Current, ct);
            }
            catch (Exception ex)
            {
                throw new global::PureES.RehydrationException(streamId, AggregateType, "PureES.Tests.Models.TestAggregate.GlobalWhen(PureES.EventEnvelope, System.Threading.CancellationToken)", ex);
            }
            try
            {
                await current.GlobalWhenAsync(enumerator.Current, services.S0);
            }
            catch (Exception ex)
            {
                throw new global::PureES.RehydrationException(streamId, AggregateType, "PureES.Tests.Models.TestAggregate.GlobalWhenAsync(PureES.EventEnvelope, Microsoft.Extensions.Logging.ILoggerFactory)", ex);
            }
            return new global::PureES.RehydratedAggregate<global::PureES.Tests.Models.TestAggregate>(current, 0u);
        }

        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        private async Task<global::PureES.RehydratedAggregate<global::PureES.Tests.Models.TestAggregate>> UpdateWhen(string streamId, global::PureES.RehydratedAggregate<global::PureES.Tests.Models.TestAggregate> aggregate, global::System.Collections.Generic.IAsyncEnumerator<global::PureES.EventEnvelope> enumerator, TestAggregateFactory.Services services, CancellationToken ct)
        {
            global::PureES.Tests.Models.TestAggregate current = aggregate.Aggregate;
            var revision = aggregate.StreamPosition;
            while (await enumerator.MoveNextAsync())
            {
                switch (enumerator.Current.Event)
                {
                    case global::PureES.Tests.Models.Events.Updated e:
                    {
                        try
                        {
                            current.When(e, services.S1);
                        }
                        catch (Exception ex)
                        {
                            throw new global::PureES.RehydrationException(streamId, AggregateType, "PureES.Tests.Models.TestAggregate.When(PureES.Tests.Models.Events.Updated, System.IServiceProvider)", ex);
                        }
                        break;
                    }
                    case int e:
                    {
                        try
                        {
                            await current.When(
                                new global::PureES.Tests.Models.EventEnvelope<int>(enumerator.Current),
                                services.S0);
                        }
                        catch (Exception ex)
                        {
                            throw new global::PureES.RehydrationException(streamId, AggregateType, "PureES.Tests.Models.TestAggregate.When(PureES.Tests.Models.EventEnvelope<int>, Microsoft.Extensions.Logging.ILoggerFactory)", ex);
                        }
                        break;
                    }
                    case global::PureES.Tests.Models.Events.Updated e:
                    {
                        try
                        {
                            current = global::PureES.Tests.Models.TestAggregate.UpdateWhenStatic(e, current);
                        }
                        catch (Exception ex)
                        {
                            throw new global::PureES.RehydrationException(streamId, AggregateType, "PureES.Tests.Models.TestAggregate.UpdateWhenStatic(PureES.Tests.Models.Events.Updated, PureES.Tests.Models.TestAggregate)", ex);
                        }
                        break;
                    }
                    default:
                    {
                        var eventType = global::PureES.BasicEventTypeMap.GetTypeName(enumerator.Current.Event.GetType());
                        throw new global::PureES.RehydrationException(streamId, AggregateType, $"No suitable UpdateWhen method found for event '{eventType}'");
                    }
                }
                try
                {
                    current.GlobalWhen(enumerator.Current, ct);
                }
                catch (Exception ex)
                {
                    throw new global::PureES.RehydrationException(streamId, AggregateType, "PureES.Tests.Models.TestAggregate.GlobalWhen(PureES.EventEnvelope, System.Threading.CancellationToken)", ex);
                }
                try
                {
                    await current.GlobalWhenAsync(enumerator.Current, services.S0);
                }
                catch (Exception ex)
                {
                    throw new global::PureES.RehydrationException(streamId, AggregateType, "PureES.Tests.Models.TestAggregate.GlobalWhenAsync(PureES.EventEnvelope, Microsoft.Extensions.Logging.ILoggerFactory)", ex);
                }
                ++revision;
            }
            return new global::PureES.RehydratedAggregate<global::PureES.Tests.Models.TestAggregate>(current, revision);
        }

        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public async Task<global::PureES.RehydratedAggregate<global::PureES.Tests.Models.TestAggregate>> Create(string streamId, global::System.Collections.Generic.IAsyncEnumerable<global::PureES.EventEnvelope> @events, CancellationToken cancellationToken)
        {
            var services = this._services.GetRequiredService<TestAggregateFactory.Services>();
            await using (var enumerator = @events.GetAsyncEnumerator(cancellationToken))
            {
                var current = await CreateWhen(streamId, enumerator, services, cancellationToken);
                return await UpdateWhen(streamId, current, enumerator, services, cancellationToken);
            }
        }

        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public async Task<global::PureES.RehydratedAggregate<global::PureES.Tests.Models.TestAggregate>> Update(string streamId, global::PureES.RehydratedAggregate<global::PureES.Tests.Models.TestAggregate> aggregate, global::System.Collections.Generic.IAsyncEnumerable<global::PureES.EventEnvelope> @events, CancellationToken cancellationToken)
        {
            var services = this._services.GetRequiredService<TestAggregateFactory.Services>();
            await using (var enumerator = @events.GetAsyncEnumerator(cancellationToken))
            {
                return await UpdateWhen(streamId, aggregate, enumerator, services, cancellationToken);
            }
        }

        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
        [global::System.CodeDom.Compiler.GeneratedCodeAttribute("PureES.SourceGenerator", "1.0.0.0")]
        internal sealed class Services
        {
            public readonly global::Microsoft.Extensions.Logging.ILoggerFactory S0;
            public readonly global::System.IServiceProvider S1;


            [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
            [global::System.Diagnostics.DebuggerStepThroughAttribute()]
            [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
            public Services(
                global::Microsoft.Extensions.Logging.ILoggerFactory s0,
                global::System.IServiceProvider s1)
            {
                this.S0 = s0;
                this.S1 = s1;
            }
        }
    }
}
