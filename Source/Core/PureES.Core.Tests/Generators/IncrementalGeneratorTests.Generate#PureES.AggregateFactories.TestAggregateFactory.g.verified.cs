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
    internal class TestAggregateFactory : global::PureES.Core.IAggregateFactory<global::PureES.Core.Tests.Models.TestAggregate>
    {
        private readonly global::PureES.Core.IEventStore _eventStore;
        private readonly global::System.IServiceProvider _services;

        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public TestAggregateFactory(
            global::PureES.Core.IEventStore eventStore,
            global::System.IServiceProvider services)
        {
            this._eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
            this._services = services ?? throw new ArgumentNullException(nameof(services));
        }
        private static readonly global::System.Type AggregateType = typeof(global::PureES.Core.Tests.Models.TestAggregate);

        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        private async Task<global::PureES.Core.Tests.Models.TestAggregate> CreateWhen(string streamId, global::System.Collections.Generic.IAsyncEnumerator<global::PureES.Core.EventEnvelope> enumerator, TestAggregateFactory_Services services, CancellationToken ct)
        {
            if (!await enumerator.MoveNextAsync())
            {
                throw new ArgumentException("Stream is empty");
            }
            global::PureES.Core.Tests.Models.TestAggregate current;
            switch (enumerator.Current.Event)
            {
                case global::PureES.Core.Tests.Models.Events.Created e:
                {
                    current = global::PureES.Core.Tests.Models.TestAggregate.When(
                        new global::PureES.Core.EventEnvelope<global::PureES.Core.Tests.Models.Events.Created, global::PureES.Core.Tests.Models.Metadata>(enumerator.Current));
                    break;
                }
                default:
                {
                    var eventType = global::PureES.Core.BasicEventTypeMap.GetTypeName(enumerator.Current.Event.GetType());
                    throw new global::PureES.Core.RehydrationException(streamId, AggregateType, $"No suitable CreateWhen method found for event '{eventType}'");
                }
            }
            current.GlobalWhen(enumerator.Current, ct);
            await current.GlobalWhenAsync(enumerator.Current, services.S0);
            return current;
        }

        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        private async Task<global::PureES.Core.Tests.Models.TestAggregate> UpdateWhen(string streamId, global::PureES.Core.Tests.Models.TestAggregate current, global::System.Collections.Generic.IAsyncEnumerator<global::PureES.Core.EventEnvelope> enumerator, TestAggregateFactory_Services services, CancellationToken ct)
        {
            while (await enumerator.MoveNextAsync())
            {
                switch (enumerator.Current.Event)
                {
                    case global::PureES.Core.Tests.Models.Events.Updated e:
                    {
                        current.When(e, services.S1);
                        break;
                    }
                    case int e:
                    {
                        await current.When(
                            new global::PureES.Core.Tests.Models.EventEnvelope<int>(enumerator.Current),
                            services.S0);
                        break;
                    }
                    case global::PureES.Core.Tests.Models.Events.Updated e:
                    {
                        current = global::PureES.Core.Tests.Models.TestAggregate.UpdateWhenStatic(e, current);
                        break;
                    }
                    default:
                    {
                        var eventType = global::PureES.Core.BasicEventTypeMap.GetTypeName(enumerator.Current.Event.GetType());
                        throw new global::PureES.Core.RehydrationException(streamId, AggregateType, $"No suitable UpdateWhen method found for event '{eventType}'");
                    }
                }
                current.GlobalWhen(enumerator.Current, ct);
                await current.GlobalWhenAsync(enumerator.Current, services.S0);
            }
            return current;
        }

        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public async Task<global::PureES.Core.Tests.Models.TestAggregate> Create(string streamId, global::System.Collections.Generic.IAsyncEnumerable<global::PureES.Core.EventEnvelope> @events, CancellationToken cancellationToken)
        {
            try
            {
                var services = this._services.GetRequiredService<TestAggregateFactory_Services>();
                await using (var enumerator = @events.GetAsyncEnumerator(cancellationToken))
                {
                    var current = await CreateWhen(streamId, enumerator, services, cancellationToken);
                    return await UpdateWhen(streamId, current, enumerator, services, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                throw new global::PureES.Core.RehydrationException(streamId, AggregateType, ex);
            }
        }

        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public async Task<global::PureES.Core.Tests.Models.TestAggregate> Update(string streamId, global::PureES.Core.Tests.Models.TestAggregate current, global::System.Collections.Generic.IAsyncEnumerable<global::PureES.Core.EventEnvelope> @events, CancellationToken cancellationToken)
        {
            try
            {
                var services = this._services.GetRequiredService<TestAggregateFactory_Services>();
                await using (var enumerator = @events.GetAsyncEnumerator(cancellationToken))
                {
                    return await UpdateWhen(streamId, current, enumerator, services, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                throw new global::PureES.Core.RehydrationException(streamId, AggregateType, ex);
            }
        }
    }
    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("PureES.SourceGenerator", "1.0.0.0")]
    internal class TestAggregateFactory_Services
    {
        public readonly global::Microsoft.Extensions.Logging.ILoggerFactory S0;
        public readonly global::System.IServiceProvider S1;


        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public TestAggregateFactory_Services(
            global::Microsoft.Extensions.Logging.ILoggerFactory s0,
            global::System.IServiceProvider s1)
        {
            this.S0 = s0;
            this.S1 = s1;
        }
    }
}
