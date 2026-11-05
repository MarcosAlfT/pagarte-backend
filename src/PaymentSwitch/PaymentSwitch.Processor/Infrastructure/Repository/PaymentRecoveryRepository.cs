using Microsoft.EntityFrameworkCore;
using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Infrastructure.Repository;

public sealed class PaymentRecoveryRepository(PaymentDbContext db) : IPaymentRecoveryRepository
{
    public async Task<IReadOnlyList<Payment>> ListAsync(int limit, CancellationToken cancellationToken) =>
        await db.Payments.AsNoTracking()
            .Where(payment => payment.ChargeFirst && payment.NextAttemptAt == null &&
                (payment.Status == PaymentTransactionStatus.ManualIntervention ||
                 payment.Status == PaymentTransactionStatus.ChargedAwaitingCompany ||
                 payment.Status == PaymentTransactionStatus.CompensationPending))
            .OrderBy(payment => payment.LastUpdatedAt).ThenBy(payment => payment.Id)
            .Take(limit).ToArrayAsync(cancellationToken);

    public Task<PaymentRecoveryAction?> FindAsync(string actor, string key, CancellationToken cancellationToken) =>
        db.PaymentRecoveryActions.AsNoTracking().SingleOrDefaultAsync(
            action => action.Actor == actor && action.RequestKey == key, cancellationToken);

    public Task<PaymentRecoveryAction?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.PaymentRecoveryActions.AsNoTracking().SingleOrDefaultAsync(action => action.Id == id, cancellationToken);

    public void Add(PaymentRecoveryAction action) => db.PaymentRecoveryActions.Add(action);
}
