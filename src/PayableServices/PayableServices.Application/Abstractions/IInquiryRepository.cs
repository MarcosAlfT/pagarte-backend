using PayableServices.Domain.Entities;

namespace PayableServices.Application.Abstractions;

public interface IInquiryRepository
{
    Task<AmountInquiry?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<AmountInquiry?> FindAsync(string clientId, string key, CancellationToken cancellationToken);
    void Add(AmountInquiry inquiry);
    Task<IReadOnlyCollection<Guid>> GetRecoverableAsync(DateTime now, CancellationToken cancellationToken);
}
