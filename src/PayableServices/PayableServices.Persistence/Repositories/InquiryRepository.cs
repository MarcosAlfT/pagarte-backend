using Microsoft.EntityFrameworkCore;
using PayableServices.Application.Abstractions;
using PayableServices.Domain.Entities;

namespace PayableServices.Persistence.Repositories;

public sealed class InquiryRepository(PayableServicesDbContext db) : IInquiryRepository
{
    public Task<AmountInquiry?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.AmountInquiries.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<AmountInquiry?> FindAsync(string clientId, string key, CancellationToken cancellationToken) =>
        db.AmountInquiries.SingleOrDefaultAsync(x => x.ClientId == clientId && x.IdempotencyKey == key, cancellationToken);
    public void Add(AmountInquiry inquiry) => db.AmountInquiries.Add(inquiry);
    public async Task<IReadOnlyCollection<Guid>> GetRecoverableAsync(DateTime now, CancellationToken cancellationToken) =>
        await db.AmountInquiries.AsNoTracking().Where(x => x.Outcome == InquiryOutcome.Pending && x.LeaseUntil <= now)
            .OrderBy(x => x.LeaseUntil).Take(50).Select(x => x.Id).ToListAsync(cancellationToken);
}
