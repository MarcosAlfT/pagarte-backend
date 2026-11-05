using PayableServices.Application.Abstractions;
using PayableServices.Domain.Entities;

namespace PayableServices.Application.Services;

public sealed class QuoteRouteEligibility(IPayableServiceRepository services, IPaymentRouteRepository routes,
    IQuoteConfigurationRepository configuration, ICompanyRouteConfiguration connections)
{
    public async Task<bool> IsEligibleAsync(Quote quote, string? inquiryProvider, CancellationToken cancellationToken)
    {
        var service = await services.GetByIdAsync(quote.ServiceId, cancellationToken);
        var route = await routes.GetByIdAsync(quote.RouteId, cancellationToken);
        var country = await configuration.GetCountryAsync(quote.CountryId, cancellationToken);
        if (service is not { IsActive: true, AllowsQuote: true, AllowsPayment: true } ||
            service.CountryId != quote.CountryId || (inquiryProvider is null && service.RequiresInquiry) ||
            !string.Equals(service.Currency, quote.Currency, StringComparison.OrdinalIgnoreCase) ||
            country is null || !string.Equals(country.Currency, quote.Currency, StringComparison.OrdinalIgnoreCase) ||
            country.Code.Trim().ToUpperInvariant() != quote.CountryCode ||
            route is not { IsActive: true } || route.PayableServiceId != quote.ServiceId ||
            route.RouteType != quote.RouteType ||
            route.ExternalServiceCode != quote.ExternalServiceCode || route.ExternalSourceCode != quote.ExternalSourceCode ||
            route.ProviderId != quote.ProviderId || route.PaymentNetworkId != quote.PaymentNetworkId ||
            route.CompanyPaymentEndpoint != quote.CompanyPaymentEndpoint ||
            route.CompanyRollbackEndpoint != quote.CompanyRollbackEndpoint ||
            route.CompanyStatusEndpoint != quote.CompanyStatusEndpoint ||
            route.CompanyCredentialReference != quote.CompanyCredentialReference)
            return false;

        if (inquiryProvider is null) return true;
        return route.InquiryProvider == inquiryProvider && connections.Resolve(inquiryProvider) is { } connection &&
            connection.PaymentEndpoint == quote.CompanyPaymentEndpoint &&
            connection.RollbackEndpoint == quote.CompanyRollbackEndpoint &&
            connection.StatusEndpoint == quote.CompanyStatusEndpoint &&
            connection.CredentialReference == quote.CompanyCredentialReference;
    }
}
