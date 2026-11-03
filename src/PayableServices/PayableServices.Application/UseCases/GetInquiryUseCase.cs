using System.Diagnostics.Metrics;
using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;

namespace PayableServices.Application.UseCases;

public sealed class GetInquiryUseCase(IInquiryRepository inquiries, IClock clock)
{
    private static readonly Meter Meter = new("Pagarte.InquiryHistory");
    private static readonly Counter<long> Expired = Meter.CreateCounter<long>("inquiry.expired_reads");
    public async Task<InquiryDto?> ExecuteAsync(string clientId, Guid id, CancellationToken cancellationToken = default)
    {
        var inquiry = await inquiries.GetAsync(id, cancellationToken);
        if (inquiry is null || inquiry.ClientId != clientId) return null;
        var result = InquiryDto.Map(inquiry, clock.UtcNow);
        if (result.Expired) Expired.Add(1);
        return result;
    }
}
