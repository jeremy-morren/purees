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
    internal class PureESServiceCollectionExtensions
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
            // Aggregate: PureES.Core.Tests.Models.TestAggregate. Command handlers: 5
            if (registeredServices.Contains(typeof(global::PureES.Core.IAggregateFactory<global::PureES.Core.Tests.Models.TestAggregate>)))
            {
                services.RemoveAll(typeof(global::PureES.Core.IAggregateFactory<global::PureES.Core.Tests.Models.TestAggregate>));
            }
            services.Add(new ServiceDescriptor(
                serviceType: typeof(global::PureES.Core.IAggregateFactory<global::PureES.Core.Tests.Models.TestAggregate>),
                implementationType: typeof(global::PureES.AggregateFactories.TestAggregateFactory),
                lifetime: ServiceLifetime.Transient));
            if (!registeredImplementations.Contains(typeof(global::PureES.AggregateFactories.TestAggregateFactory.Services)))
            {
                services.Add(new ServiceDescriptor(
                    serviceType: typeof(global::PureES.AggregateFactories.TestAggregateFactory.Services),
                    implementationType: typeof(global::PureES.AggregateFactories.TestAggregateFactory.Services),
                    lifetime: ServiceLifetime.Transient));
            }
            if (registeredServices.Contains(typeof(global::PureES.Core.ICommandHandler<global::PureES.Core.Tests.Models.Commands.Create>)))
            {
                services.RemoveAll(typeof(global::PureES.Core.ICommandHandler<global::PureES.Core.Tests.Models.Commands.Create>));
            }
            services.Add(new ServiceDescriptor(
                serviceType: typeof(global::PureES.Core.ICommandHandler<global::PureES.Core.Tests.Models.Commands.Create>),
                implementationType: typeof(global::PureES.CommandHandlers.CreateCommandHandler),
                lifetime: ServiceLifetime.Transient));
            if (registeredServices.Contains(typeof(global::PureES.Core.ICommandHandler<global::PureES.Core.Tests.Models.Commands.Update>)))
            {
                services.RemoveAll(typeof(global::PureES.Core.ICommandHandler<global::PureES.Core.Tests.Models.Commands.Update>));
            }
            services.Add(new ServiceDescriptor(
                serviceType: typeof(global::PureES.Core.ICommandHandler<global::PureES.Core.Tests.Models.Commands.Update>),
                implementationType: typeof(global::PureES.CommandHandlers.UpdateCommandHandler),
                lifetime: ServiceLifetime.Transient));
            if (registeredServices.Contains(typeof(global::PureES.Core.ICommandHandler<global::PureES.Core.Tests.Models.Commands.UpdateConstantStream, int[]>)))
            {
                services.RemoveAll(typeof(global::PureES.Core.ICommandHandler<global::PureES.Core.Tests.Models.Commands.UpdateConstantStream, int[]>));
            }
            services.Add(new ServiceDescriptor(
                serviceType: typeof(global::PureES.Core.ICommandHandler<global::PureES.Core.Tests.Models.Commands.UpdateConstantStream, int[]>),
                implementationType: typeof(global::PureES.CommandHandlers.UpdateConstantStreamCommandHandler),
                lifetime: ServiceLifetime.Transient));
            if (registeredServices.Contains(typeof(global::PureES.Core.ICommandHandler<int[]>)))
            {
                services.RemoveAll(typeof(global::PureES.Core.ICommandHandler<int[]>));
            }
            services.Add(new ServiceDescriptor(
                serviceType: typeof(global::PureES.Core.ICommandHandler<int[]>),
                implementationType: typeof(global::PureES.CommandHandlers.CommandHandler),
                lifetime: ServiceLifetime.Transient));
            if (registeredServices.Contains(typeof(global::PureES.Core.ICommandHandler<decimal>)))
            {
                services.RemoveAll(typeof(global::PureES.Core.ICommandHandler<decimal>));
            }
            services.Add(new ServiceDescriptor(
                serviceType: typeof(global::PureES.Core.ICommandHandler<decimal>),
                implementationType: typeof(global::PureES.CommandHandlers.DecimalCommandHandler),
                lifetime: ServiceLifetime.Transient));

            // Aggregate: PureES.Core.Tests.Models.ImplementedGenericAggregate. Command handlers: 2
            if (registeredServices.Contains(typeof(global::PureES.Core.IAggregateFactory<global::PureES.Core.Tests.Models.ImplementedGenericAggregate>)))
            {
                services.RemoveAll(typeof(global::PureES.Core.IAggregateFactory<global::PureES.Core.Tests.Models.ImplementedGenericAggregate>));
            }
            services.Add(new ServiceDescriptor(
                serviceType: typeof(global::PureES.Core.IAggregateFactory<global::PureES.Core.Tests.Models.ImplementedGenericAggregate>),
                implementationType: typeof(global::PureES.AggregateFactories.ImplementedGenericAggregateFactory),
                lifetime: ServiceLifetime.Transient));
            if (registeredServices.Contains(typeof(global::PureES.Core.ICommandHandler<object>)))
            {
                services.RemoveAll(typeof(global::PureES.Core.ICommandHandler<object>));
            }
            services.Add(new ServiceDescriptor(
                serviceType: typeof(global::PureES.Core.ICommandHandler<object>),
                implementationType: typeof(global::PureES.CommandHandlers.ObjectCommandHandler),
                lifetime: ServiceLifetime.Transient));
            if (registeredServices.Contains(typeof(global::PureES.Core.ICommandHandler<global::System.Collections.Generic.Dictionary<string, object>>)))
            {
                services.RemoveAll(typeof(global::PureES.Core.ICommandHandler<global::System.Collections.Generic.Dictionary<string, object>>));
            }
            services.Add(new ServiceDescriptor(
                serviceType: typeof(global::PureES.Core.ICommandHandler<global::System.Collections.Generic.Dictionary<string, object>>),
                implementationType: typeof(global::PureES.CommandHandlers.Dictionary_String_ObjectCommandHandler),
                lifetime: ServiceLifetime.Transient));

            // Event Handlers. Count: 7

            if (!registeredImplementations.Contains(typeof(global::PureES.EventHandlers.CreatedEventHandler_PureESCoreTestsModelsTestEventHandlers_OnCreated)))
            {
                services.Add(new ServiceDescriptor(
                    serviceType: typeof(global::PureES.Core.IEventHandler<global::PureES.Core.Tests.Models.Events.Created>),
                    implementationType: typeof(global::PureES.EventHandlers.CreatedEventHandler_PureESCoreTestsModelsTestEventHandlers_OnCreated),
                    lifetime: ServiceLifetime.Transient));
            }

            if (!registeredImplementations.Contains(typeof(global::PureES.EventHandlers.UpdatedEventHandler_PureESCoreTestsModelsTestEventHandlers_OnUpdated)))
            {
                services.Add(new ServiceDescriptor(
                    serviceType: typeof(global::PureES.Core.IEventHandler<global::PureES.Core.Tests.Models.Events.Updated>),
                    implementationType: typeof(global::PureES.EventHandlers.UpdatedEventHandler_PureESCoreTestsModelsTestEventHandlers_OnUpdated),
                    lifetime: ServiceLifetime.Transient));
            }

            if (!registeredImplementations.Contains(typeof(global::PureES.EventHandlers.CreatedEventHandler_PureESCoreTestsModelsTestEventHandlers_OnCreated2)))
            {
                services.Add(new ServiceDescriptor(
                    serviceType: typeof(global::PureES.Core.IEventHandler<global::PureES.Core.Tests.Models.Events.Created>),
                    implementationType: typeof(global::PureES.EventHandlers.CreatedEventHandler_PureESCoreTestsModelsTestEventHandlers_OnCreated2),
                    lifetime: ServiceLifetime.Transient));
            }

            if (!registeredImplementations.Contains(typeof(global::PureES.EventHandlers.CatchAllEventHandler_PureESCoreTestsModelsTestEventHandlers_CatchAll)))
            {
                services.Add(new ServiceDescriptor(
                    serviceType: typeof(global::PureES.Core.IEventHandler),
                    implementationType: typeof(global::PureES.EventHandlers.CatchAllEventHandler_PureESCoreTestsModelsTestEventHandlers_CatchAll),
                    lifetime: ServiceLifetime.Transient));
            }

            if (!registeredImplementations.Contains(typeof(global::PureES.EventHandlers.CreatedEventHandler_PureESCoreTestsModelsImplementedGenericEventHandlers_On)))
            {
                services.Add(new ServiceDescriptor(
                    serviceType: typeof(global::PureES.Core.IEventHandler<global::PureES.Core.Tests.Models.Events.Created>),
                    implementationType: typeof(global::PureES.EventHandlers.CreatedEventHandler_PureESCoreTestsModelsImplementedGenericEventHandlers_On),
                    lifetime: ServiceLifetime.Transient));
            }

            if (!registeredImplementations.Contains(typeof(global::PureES.EventHandlers.CreatedEventHandler_PureESCoreTestsModelsImplementedGenericEventHandlers_On2)))
            {
                services.Add(new ServiceDescriptor(
                    serviceType: typeof(global::PureES.Core.IEventHandler<global::PureES.Core.Tests.Models.Events.Created>),
                    implementationType: typeof(global::PureES.EventHandlers.CreatedEventHandler_PureESCoreTestsModelsImplementedGenericEventHandlers_On2),
                    lifetime: ServiceLifetime.Transient));
            }

            if (!registeredImplementations.Contains(typeof(global::PureES.EventHandlers.CreatedEventHandler_PureESCoreTestsModelsImplementedGenericEventHandlers_Async)))
            {
                services.Add(new ServiceDescriptor(
                    serviceType: typeof(global::PureES.Core.IEventHandler<global::PureES.Core.Tests.Models.Events.Created>),
                    implementationType: typeof(global::PureES.EventHandlers.CreatedEventHandler_PureESCoreTestsModelsImplementedGenericEventHandlers_Async),
                    lifetime: ServiceLifetime.Transient));
            }
            // Event handler parents. Count: 2

            if (!registeredServices.Contains(typeof(global::PureES.Core.Tests.Models.TestEventHandlers)))
            {
                services.Add(new ServiceDescriptor(
                    serviceType: typeof(global::PureES.Core.Tests.Models.TestEventHandlers),
                    implementationType: typeof(global::PureES.Core.Tests.Models.TestEventHandlers),
                    lifetime: ServiceLifetime.Transient));
            }


            if (!registeredServices.Contains(typeof(global::PureES.Core.Tests.Models.ImplementedGenericEventHandlers)))
            {
                services.Add(new ServiceDescriptor(
                    serviceType: typeof(global::PureES.Core.Tests.Models.ImplementedGenericEventHandlers),
                    implementationType: typeof(global::PureES.Core.Tests.Models.ImplementedGenericEventHandlers),
                    lifetime: ServiceLifetime.Transient));
            }

        }
    }
}
