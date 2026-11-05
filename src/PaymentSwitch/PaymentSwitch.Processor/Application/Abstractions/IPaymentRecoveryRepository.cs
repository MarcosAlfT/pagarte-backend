using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.Abstractions;

public interface IPaymentRecoveryRepository
{
    Task<IReadOnlyList<Payment>> ListAsync(int limit, CancellationToken cancellationToken);
    Task<PaymentRecoveryAction?> FindAsync(string actor, string key, CancellationToken cancellationToken);
    Task<PaymentRecoveryAction?> GetAsync(Guid id, CancellationToken cancellationToken);
    void Add(PaymentRecoveryAction action);
}
