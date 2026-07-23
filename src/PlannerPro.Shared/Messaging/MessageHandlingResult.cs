namespace PlannerPro.Shared.Messaging;

/// <summary>
/// What <see cref="ServiceBusProcessorHost.HandleAsync"/> decided to do with a message. The outer,
/// SDK-facing <c>ProcessMessageAsync</c> wrapper turns this into the actual
/// <c>CompleteMessageAsync</c>/<c>DeadLetterMessageAsync</c> call.
/// </summary>
internal readonly record struct MessageHandlingResult(
    MessageDisposition Disposition,
    string? Reason = null,
    string? Description = null)
{
    public static MessageHandlingResult Complete { get; } = new(MessageDisposition.Complete);

    public static MessageHandlingResult DeadLetter(string reason, string description) =>
        new(MessageDisposition.DeadLetter, reason, description);
}
