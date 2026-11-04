using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.Abstractions;

public interface ICardDetachmentRepository
{
	Task<CreditCard?> GetAsync(Guid id, CancellationToken cancellationToken);
	Task<IReadOnlyList<Guid>> GetDueAsync(DateTime utcNow, CancellationToken cancellationToken);
	Task<bool> HasRegistrationBarrierAsync(string owner, CancellationToken cancellationToken);
	Task<bool> HasDependenciesAsync(Guid cardId, bool refundsSurviveDetachment, CancellationToken cancellationToken);
}
