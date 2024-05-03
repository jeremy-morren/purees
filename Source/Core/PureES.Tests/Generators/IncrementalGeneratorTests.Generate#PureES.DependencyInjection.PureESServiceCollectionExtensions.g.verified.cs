﻿//HintName: PureES.DependencyInjection.PureESServiceCollectionExtensions.g.cs
// <auto-generated/>

// This file was automatically generated by the PureES source generator.
// Do not edit this file manually since it will be automatically overwritten.
// ReSharper disable All

#nullable enable
#pragma warning disable CS0162 //Unreachable code detected

#pragma warning disable CS8019 //Unnecessary using directive
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PureES.DependencyInjection
{
    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("PureES.SourceGenerator", "1.0.0.0")]
    internal static class PureESServiceCollectionExtensions
    {

        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]
        [global::System.Diagnostics.DebuggerStepThroughAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        private static void Register(IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var registeredServices = new global::System.Collections.Generic.HashSet<global::System.Type>();
            var registeredImplementations = new global::System.Collections.Generic.HashSet<global::System.Type>();
            foreach (var s in services)
            {
                registeredServices.Add(s.ServiceType);
                if (s.ImplementationType != null)
                {
                    registeredImplementations.Add(s.ImplementationType);
                }
            }
            // Aggregate: PureES.Tests.Models.TestAggregate. Command handlers: 9
            if (registeredServices.Contains(typeof(global::PureES.IAggregateFactory<global::PureES.Tests.Models.TestAggregate>)))
            {
                services.RemoveAll(typeof(global::PureES.IAggregateFactory<global::PureES.Tests.Models.TestAggregate>));
            }
            services.Add(new ServiceDescriptor(
                serviceType: typeof(global::PureES.IAggregateFactory<global::PureES.Tests.Models.TestAggregate>),
                implementationType: typeof(global::PureES.AggregateFactories.TestAggregateFactory),
                lifetime: ServiceLifetime.Transient));
            if (!registeredImplementations.Contains(typeof(global::PureES.AggregateFactories.TestAggregateFactory.Services)))
            {
                services.Add(new ServiceDescriptor(
                    serviceType: typeof(global::PureES.AggregateFactories.TestAggregateFactory.Services),
                    implementationType: typeof(global::PureES.AggregateFactories.TestAggregateFactory.Services),
                    lifetime: ServiceLifetime.Transient));
            }
            if (registeredServices.Contains(typeof(global::PureES.ICommandHandler<global::PureES.Tests.Models.Commands.Create>)))
            {
                services.RemoveAll(typeof(global::PureES.ICommandHandler<global::PureES.Tests.Models.Commands.Create>));
            }
            services.Add(new ServiceDescriptor(
                serviceType: typeof(global::PureES.ICommandHandler<global::PureES.Tests.Models.Commands.Create>),
                implementationType: typeof(global::PureES.CommandHandlers.Commands_CreateCommandHandler),
                lifetime: ServiceLifetime.Transient));
            if (registeredServices.Contains(typeof(global::PureES.ICommandHandler<global::PureES.Tests.Models.Commands.Update>)))
            {
                services.RemoveAll(typeof(global::PureES.ICommandHandler<global::PureES.Tests.Models.Commands.Update>));
            }
            services.Add(new ServiceDescriptor(
                serviceType: typeof(global::PureES.ICommandHandler<global::PureES.Tests.Models.Commands.Update>),
                implementationType: typeof(global::PureES.CommandHandlers.Commands_UpdateCommandHandler),
                lifetime: ServiceLifetime.Transient));
            if (registeredServices.Contains(typeof(global::PureES.ICommandHandler<global::PureES.Tests.Models.Commands.UpdateConstantStream, int[]>)))
            {
                services.RemoveAll(typeof(global::PureES.ICommandHandler<global::PureES.Tests.Models.Commands.UpdateConstantStream, int[]>));
            }
            services.Add(new ServiceDescriptor(
                serviceType: typeof(global::PureES.ICommandHandler<global::PureES.Tests.Models.Commands.UpdateConstantStream, int[]>),
                implementationType: typeof(global::PureES.CommandHandlers.Commands_UpdateConstantStreamCommandHandler),
                lifetime: ServiceLifetime.Transient));
            if (registeredServices.Contains(typeof(global::PureES.ICommandHandler<int[]>)))
            {
                services.RemoveAll(typeof(global::PureES.ICommandHandler<int[]>));
            }
            services.Add(new ServiceDescriptor(
                serviceType: typeof(global::PureES.ICommandHandler<int[]>),
                implementationType: typeof(global::PureES.CommandHandlers.intArrayCommandHandler),
                lifetime: ServiceLifetime.Transient));
            if (registeredServices.Contains(typeof(global::PureES.ICommandHandler<decimal>)))
            {
                services.RemoveAll(typeof(global::PureES.ICommandHandler<decimal>));
            }
            services.Add(new ServiceDescriptor(
                serviceType: typeof(global::PureES.ICommandHandler<decimal>),
                implementationType: typeof(global::PureES.CommandHandlers.decimalCommandHandler),
                lifetime: ServiceLifetime.Transient));
            if (registeredServices.Contains(typeof(global::PureES.ICommandHandler<ulong>)))
            {
                services.RemoveAll(typeof(global::PureES.ICommandHandler<ulong>));
            }
            services.Add(new ServiceDescriptor(
                serviceType: typeof(global::PureES.ICommandHandler<ulong>),
                implementationType: typeof(global::PureES.CommandHandlers.ulongCommandHandler),
                lifetime: ServiceLifetime.Transient));
            if (registeredServices.Contains(typeof(global::PureES.ICommandHandler<ushort>)))
            {
                services.RemoveAll(typeof(global::PureES.ICommandHandler<ushort>));
            }
            services.Add(new ServiceDescriptor(
                serviceType: typeof(global::PureES.ICommandHandler<ushort>),
                implementationType: typeof(global::PureES.CommandHandlers.ushortCommandHandler),
                lifetime: ServiceLifetime.Transient));
            if (registeredServices.Contains(typeof(global::PureES.ICommandHandler<short>)))
            {
                services.RemoveAll(typeof(global::PureES.ICommandHandler<short>));
            }
            services.Add(new ServiceDescriptor(
                serviceType: typeof(global::PureES.ICommandHandler<short>),
                implementationType: typeof(global::PureES.CommandHandlers.shortCommandHandler),
                lifetime: ServiceLifetime.Transient));
            if (registeredServices.Contains(typeof(global::PureES.ICommandHandler<long[], object>)))
            {
                services.RemoveAll(typeof(global::PureES.ICommandHandler<long[], object>));
            }
            services.Add(new ServiceDescriptor(
                serviceType: typeof(global::PureES.ICommandHandler<long[], object>),
                implementationType: typeof(global::PureES.CommandHandlers.longArrayCommandHandler),
                lifetime: ServiceLifetime.Transient));

            // Aggregate: PureES.Tests.Models.ImplementedGenericAggregate. Command handlers: 2
            if (registeredServices.Contains(typeof(global::PureES.IAggregateFactory<global::PureES.Tests.Models.ImplementedGenericAggregate>)))
            {
                services.RemoveAll(typeof(global::PureES.IAggregateFactory<global::PureES.Tests.Models.ImplementedGenericAggregate>));
            }
            services.Add(new ServiceDescriptor(
                serviceType: typeof(global::PureES.IAggregateFactory<global::PureES.Tests.Models.ImplementedGenericAggregate>),
                implementationType: typeof(global::PureES.AggregateFactories.ImplementedGenericAggregateFactory),
                lifetime: ServiceLifetime.Transient));
            if (registeredServices.Contains(typeof(global::PureES.ICommandHandler<object>)))
            {
                services.RemoveAll(typeof(global::PureES.ICommandHandler<object>));
            }
            services.Add(new ServiceDescriptor(
                serviceType: typeof(global::PureES.ICommandHandler<object>),
                implementationType: typeof(global::PureES.CommandHandlers.objectCommandHandler),
                lifetime: ServiceLifetime.Transient));
            if (registeredServices.Contains(typeof(global::PureES.ICommandHandler<global::System.Collections.Generic.Dictionary<string, object>>)))
            {
                services.RemoveAll(typeof(global::PureES.ICommandHandler<global::System.Collections.Generic.Dictionary<string, object>>));
            }
            services.Add(new ServiceDescriptor(
                serviceType: typeof(global::PureES.ICommandHandler<global::System.Collections.Generic.Dictionary<string, object>>),
                implementationType: typeof(global::PureES.CommandHandlers.Dictionary_string_object_CommandHandler),
                lifetime: ServiceLifetime.Transient));

            // Event Handlers. Count: 7

            if (!registeredImplementations.Contains(typeof(global::PureES.EventHandlers.Events_CreatedEventHandler_PureESTestsModelsTestEventHandlers_OnCreated)))
            {
                services.Add(new ServiceDescriptor(
                    serviceType: typeof(global::PureES.IEventHandler<global::PureES.Tests.Models.Events.Created>),
                    implementationType: typeof(global::PureES.EventHandlers.Events_CreatedEventHandler_PureESTestsModelsTestEventHandlers_OnCreated),
                    lifetime: ServiceLifetime.Transient));
            }

            if (!registeredImplementations.Contains(typeof(global::PureES.EventHandlers.Events_UpdatedEventHandler_PureESTestsModelsTestEventHandlers_OnUpdated)))
            {
                services.Add(new ServiceDescriptor(
                    serviceType: typeof(global::PureES.IEventHandler<global::PureES.Tests.Models.Events.Updated>),
                    implementationType: typeof(global::PureES.EventHandlers.Events_UpdatedEventHandler_PureESTestsModelsTestEventHandlers_OnUpdated),
                    lifetime: ServiceLifetime.Transient));
            }

            if (!registeredImplementations.Contains(typeof(global::PureES.EventHandlers.Events_CreatedEventHandler_PureESTestsModelsTestEventHandlers_OnCreated2)))
            {
                services.Add(new ServiceDescriptor(
                    serviceType: typeof(global::PureES.IEventHandler<global::PureES.Tests.Models.Events.Created>),
                    implementationType: typeof(global::PureES.EventHandlers.Events_CreatedEventHandler_PureESTestsModelsTestEventHandlers_OnCreated2),
                    lifetime: ServiceLifetime.Transient));
            }

            if (!registeredImplementations.Contains(typeof(global::PureES.EventHandlers.CatchAllEventHandler_PureESTestsModelsTestEventHandlers_CatchAll)))
            {
                services.Add(new ServiceDescriptor(
                    serviceType: typeof(global::PureES.IEventHandler),
                    implementationType: typeof(global::PureES.EventHandlers.CatchAllEventHandler_PureESTestsModelsTestEventHandlers_CatchAll),
                    lifetime: ServiceLifetime.Transient));
            }

            if (!registeredImplementations.Contains(typeof(global::PureES.EventHandlers.Events_CreatedEventHandler_PureESTestsModelsImplementedGenericEventHandlers_On)))
            {
                services.Add(new ServiceDescriptor(
                    serviceType: typeof(global::PureES.IEventHandler<global::PureES.Tests.Models.Events.Created>),
                    implementationType: typeof(global::PureES.EventHandlers.Events_CreatedEventHandler_PureESTestsModelsImplementedGenericEventHandlers_On),
                    lifetime: ServiceLifetime.Transient));
            }

            if (!registeredImplementations.Contains(typeof(global::PureES.EventHandlers.Events_CreatedEventHandler_PureESTestsModelsImplementedGenericEventHandlers_On2)))
            {
                services.Add(new ServiceDescriptor(
                    serviceType: typeof(global::PureES.IEventHandler<global::PureES.Tests.Models.Events.Created>),
                    implementationType: typeof(global::PureES.EventHandlers.Events_CreatedEventHandler_PureESTestsModelsImplementedGenericEventHandlers_On2),
                    lifetime: ServiceLifetime.Transient));
            }

            if (!registeredImplementations.Contains(typeof(global::PureES.EventHandlers.Events_CreatedEventHandler_PureESTestsModelsImplementedGenericEventHandlers_Async)))
            {
                services.Add(new ServiceDescriptor(
                    serviceType: typeof(global::PureES.IEventHandler<global::PureES.Tests.Models.Events.Created>),
                    implementationType: typeof(global::PureES.EventHandlers.Events_CreatedEventHandler_PureESTestsModelsImplementedGenericEventHandlers_Async),
                    lifetime: ServiceLifetime.Transient));
            }
            // Event handler parents. Count: 2

            if (!registeredServices.Contains(typeof(global::PureES.Tests.Models.TestEventHandlers)))
            {
                services.Add(new ServiceDescriptor(
                    serviceType: typeof(global::PureES.Tests.Models.TestEventHandlers),
                    implementationType: typeof(global::PureES.Tests.Models.TestEventHandlers),
                    lifetime: ServiceLifetime.Transient));
            }


            if (!registeredServices.Contains(typeof(global::PureES.Tests.Models.ImplementedGenericEventHandlers)))
            {
                services.Add(new ServiceDescriptor(
                    serviceType: typeof(global::PureES.Tests.Models.ImplementedGenericEventHandlers),
                    implementationType: typeof(global::PureES.Tests.Models.ImplementedGenericEventHandlers),
                    lifetime: ServiceLifetime.Transient));
            }

        }
    }
}
