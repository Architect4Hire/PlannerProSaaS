using Azure.Messaging.ServiceBus;

namespace PlannerPro.Shared.Tests.TestSupport;

/// <summary>
/// <see cref="ServiceBusSender"/> ships a protected parameterless constructor and virtual send
/// methods specifically to support subclassing like this for tests — no mocking framework needed.
/// </summary>
internal sealed class FakeServiceBusSender : ServiceBusSender
{
    public List<ServiceBusMessage> SentMessages { get; } = [];

    /// <summary>When set, the next <see cref="SendMessageAsync(ServiceBusMessage, CancellationToken)"/>
    /// throws this instead of recording the message, then clears itself.</summary>
    public Exception? ThrowOnNextSend { get; set; }

    public override Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
    {
        if (ThrowOnNextSend is { } ex)
        {
            ThrowOnNextSend = null;
            throw ex;
        }

        SentMessages.Add(message);
        return Task.CompletedTask;
    }
}
