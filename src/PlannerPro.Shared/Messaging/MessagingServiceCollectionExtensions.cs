using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlannerPro.Contracts;
using PlannerPro.Shared.Persistence;

namespace PlannerPro.Shared.Messaging;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TConsumer"/> to handle <typeparamref name="TEvent"/> and adds it
    /// to the registry <see cref="ServiceBusProcessorHost"/> dispatches through. Call once per
    /// (event type, consumer) pair a service consumes.
    /// </summary>
    public static IServiceCollection AddIntegrationEventConsumer<TEvent, TConsumer>(this IServiceCollection services)
        where TEvent : class, IIntegrationEvent
        where TConsumer : class, IIntegrationEventConsumer<TEvent>
    {
        services.TryAddScoped<IIntegrationEventConsumer<TEvent>, TConsumer>();
        services.AddSingleton<IIntegrationEventRegistration, IntegrationEventRegistration<TEvent>>();
        services.TryAddSingleton<IntegrationEventConsumerRegistry>();
        return services;
    }

    /// <summary>
    /// Registers the <see cref="OutboxDispatcher{TContext}"/> background service that sends
    /// <typeparamref name="TContext"/>'s outbox to <paramref name="topicName"/>. Requires a
    /// <see cref="ServiceBusClient"/> already registered in DI — by the host, via Aspire's
    /// <c>AddAzureServiceBusClient("servicebus")</c>, never constructed here. Call once per service
    /// (one outbox per service database).
    /// </summary>
    public static IServiceCollection AddOutboxDispatcher<TContext>(
        this IServiceCollection services, string topicName, TimeSpan? pollInterval = null)
        where TContext : SharedDbContext
    {
        if (services.Any(d => d.ServiceType == typeof(ServiceBusSender)))
        {
            throw new InvalidOperationException(
                $"{nameof(AddOutboxDispatcher)} was already called for this service. Only one outbox " +
                "dispatcher is supported per service — a second registration would silently replace " +
                "the first ServiceBusSender, wiring the wrong topic.");
        }

        services.AddSingleton(sp => sp.GetRequiredService<ServiceBusClient>().CreateSender(topicName));
        services.AddHostedService(sp => new OutboxDispatcher<TContext>(
            sp.GetRequiredService<ServiceBusSender>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger<OutboxDispatcher<TContext>>>(),
            pollInterval ?? TimeSpan.FromSeconds(5)));
        return services;
    }

    /// <summary>
    /// Registers a <see cref="ServiceBusProcessorHost"/> background service receiving on
    /// (<paramref name="topicName"/>, <paramref name="subscriptionName"/>). Safe to call more than
    /// once with different topic/subscription pairs — each gets its own hosted-service instance via a
    /// factory closure, sharing the one <see cref="IntegrationEventConsumerRegistry"/>. Requires a
    /// <see cref="ServiceBusClient"/> already registered in DI, same as
    /// <see cref="AddOutboxDispatcher{TContext}"/>.
    /// </summary>
    public static IServiceCollection AddServiceBusProcessorHost(
        this IServiceCollection services, string topicName, string subscriptionName)
    {
        services.TryAddSingleton<IntegrationEventConsumerRegistry>();
        services.AddSingleton<IHostedService>(sp => new ServiceBusProcessorHost(
            sp.GetRequiredService<ServiceBusClient>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IntegrationEventConsumerRegistry>(),
            sp.GetRequiredService<ILogger<ServiceBusProcessorHost>>(),
            topicName,
            subscriptionName));
        return services;
    }
}
