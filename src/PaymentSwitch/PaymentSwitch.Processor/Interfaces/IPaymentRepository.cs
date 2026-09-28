using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Interfaces;

public interface IPaymentRepository
{
	Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task<Payment?> GetBySourceQuoteIdAsync(
		Guid sourceQuoteId,
		CancellationToken cancellationToken = default);
	Task<IReadOnlyCollection<Payment>> GetByClientIdAsync(
		string clientId,
		int page,
		int pageSize,
		CancellationToken cancellationToken = default);
	Task<int> GetCountByClientIdAsync(
		string clientId,
		CancellationToken cancellationToken = default);
	Task<IReadOnlyCollection<Payment>> GetDueAsync(
		DateTime utcNow,
		int batchSize,
		CancellationToken cancellationToken = default);
	Task<PaymentInsertResult> InsertIfSourceQuoteAbsentAsync(
		Payment payment,
		CancellationToken cancellationToken = default);
	Task UpdateAsync(
		Payment payment,
		CancellationToken cancellationToken = default);
}

public sealed record PaymentInsertResult(bool Inserted, Payment Payment);
