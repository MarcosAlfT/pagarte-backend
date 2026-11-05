using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Application.UseCases;

public sealed record PaymentRecoveryItem(Guid PaymentId, string Reference, PaymentRecoveryStage Stage,
    string Version, DateTime UpdatedAt);

public sealed record SchedulePaymentRecovery(Guid PaymentId, string Actor, string RequestKey,
    string ExpectedVersion, string ReasonCode);

public sealed class PaymentRecoveryException(string code, int status) : Exception(code)
{
    public int Status { get; } = status;
}

public sealed class ReviewPaymentRecoveryUseCase(IPaymentRecoveryRepository reviews,
    IPaymentRepository payments, ICardMutationScope mutations, IClock clock)
{
    public async Task<IReadOnlyList<PaymentRecoveryItem>> ListAsync(int limit, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 100) throw new PaymentRecoveryException("INVALID_RECOVERY_LIMIT", 400);
        return (await reviews.ListAsync(limit, cancellationToken)).Select(payment =>
            new PaymentRecoveryItem(payment.Id, payment.Reference, payment.GetRecoveryStage()!.Value,
                Convert.ToBase64String(payment.Version), payment.LastUpdatedAt ?? payment.CreatedAt)).ToArray();
    }

    public async Task<PaymentRecoveryAction> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await reviews.GetAsync(id, cancellationToken) ?? throw new PaymentRecoveryException("RECOVERY_AUDIT_NOT_FOUND", 404);

    public async Task<PaymentRecoveryAction> ScheduleAsync(SchedulePaymentRecovery command, CancellationToken cancellationToken)
    {
        if (command.PaymentId == Guid.Empty || string.IsNullOrWhiteSpace(command.Actor) || command.Actor.Length > 300 ||
            command.Actor.Any(char.IsControl) || command.RequestKey.Length is < 1 or > 100 ||
            command.RequestKey.Any(character => character is < '!' or > '~') || command.ExpectedVersion.Length != 12 ||
            !Convert.TryFromBase64String(command.ExpectedVersion, new byte[8], out var versionLength) || versionLength != 8 ||
            command.ReasonCode is not ("ProviderIncidentResolved" or "ProviderStatusAvailable"))
            throw new PaymentRecoveryException("INVALID_RECOVERY_REQUEST", 400);

        var recorded = await reviews.FindAsync(command.Actor, command.RequestKey, cancellationToken);
        if (recorded is not null) return Replay(recorded, command);
        var initial = await payments.GetByIdAsync(command.PaymentId, cancellationToken)
            ?? throw new PaymentRecoveryException("PAYMENT_NOT_FOUND", 404);

        await using var transaction = await mutations.BeginAsync(initial.ClientId, cancellationToken);
        recorded = await reviews.FindAsync(command.Actor, command.RequestKey, cancellationToken);
        if (recorded is not null) return Replay(recorded, command);
        var payment = await payments.GetByIdAsync(command.PaymentId, cancellationToken)
            ?? throw new PaymentRecoveryException("PAYMENT_NOT_FOUND", 404);
        if (payment.GetRecoveryStage() is not { } stage || Convert.ToBase64String(payment.Version) != command.ExpectedVersion)
            throw new PaymentRecoveryException("PAYMENT_RECOVERY_STATE_CHANGED", 409);

        var now = clock.UtcNow;
        payment.ScheduleRecovery(stage, now);
        var action = new PaymentRecoveryAction
        {
            Id = Guid.NewGuid(), PaymentId = payment.Id, Actor = command.Actor, RequestKey = command.RequestKey,
            ExpectedVersion = command.ExpectedVersion, ReasonCode = command.ReasonCode, Stage = stage, ScheduledAt = now
        };
        reviews.Add(action);
        await transaction.CommitAsync(cancellationToken);
        return action;
    }

    private static PaymentRecoveryAction Replay(PaymentRecoveryAction action, SchedulePaymentRecovery command) =>
        action.PaymentId == command.PaymentId && action.ExpectedVersion == command.ExpectedVersion &&
        action.ReasonCode == command.ReasonCode ? action : throw new PaymentRecoveryException("RECOVERY_KEY_CONFLICT", 409);
}
