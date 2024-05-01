﻿using JetBrains.Annotations;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using PureES.EventBus;
using PureES.EventStores.Marten.Subscriptions;

namespace PureES.EventStores.Marten;

[PublicAPI]
public static class MartenEventStoreServiceCollectionExtensions
{
    public static MartenServiceCollectionExtensions.MartenConfigurationExpression AddPureESEventStore(
        this MartenServiceCollectionExtensions.MartenConfigurationExpression configuration,
        Action<MartenEventStoreOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        configuration.Services.AddOptions<MartenEventStoreOptions>()
            .Configure(o => configureOptions?.Invoke(o))
            .Validate(o => o.Validate());

        configuration.Services.AddSingleton<IConfigureMarten, EventStoreConfigureMarten>();
        configuration.Services.AddSingleton<IEventStore, MartenEventStore>();
        configuration.Services.AddSingleton<MartenEventSerializer>();

        return configuration;
    }

    public static MartenServiceCollectionExtensions.MartenConfigurationExpression AddPureESSubscriptionToAll(
        this MartenServiceCollectionExtensions.MartenConfigurationExpression configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        configuration.Services.AddHostedService<MartenSubscriptionToAll>();

        return configuration;
    }
}