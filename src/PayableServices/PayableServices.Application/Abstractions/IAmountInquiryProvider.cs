using PayableServices.Application.Models;

namespace PayableServices.Application.Abstractions;

public interface IAmountInquiryProvider
{
    bool IsConfigured(string provider);
    Task<ProviderInquiryResult> InquireAsync(ProviderInquiryRequest request, CancellationToken cancellationToken);
}
