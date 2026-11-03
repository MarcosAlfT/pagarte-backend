using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;
using PayableServices.Domain.Enums;

namespace PayableServices.Application.UseCases;

public sealed class ActivatePaymentRouteUseCase(IPaymentRouteRepository routes,
    IPayableServiceRepository services, IQuoteConfigurationRepository configuration,
    ICatalogueImportRepository imports, IAmountInquiryProvider provider,
    ICompanyRouteConfiguration connections, IUnitOfWork unitOfWork)
{
    public async Task<ActivatePaymentRouteResult> ExecuteAsync(ActivatePaymentRouteCommand request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginAsync("catalogue", cancellationToken);
        var route = await routes.GetByIdAsync(request.PaymentRouteId, cancellationToken);
        if (route is null) return Failure("Payment route not found.");
        var service = await services.GetByIdAsync(route.PayableServiceId, cancellationToken);
        var mapping = await imports.GetRouteMappingAsync(route.Id, cancellationToken);
        var item = mapping is null ? null : await imports.GetItemAsync(mapping.ExternalCatalogueItemId, cancellationToken);
        if (service is null || mapping?.PayableServiceId != service.Id || item is not { IsAvailable: true } ||
            item.ExternalCode != route.ExternalServiceCode || route.ProviderId != service.ProviderId)
            return Failure("A valid available catalogue mapping is required.");
        var sourceName = await imports.GetSourceNameAsync(item.ExternalCatalogueSourceId, cancellationToken);
        if (sourceName is null || sourceName != route.ExternalSourceCode || sourceName != route.InquiryProvider)
            return Failure("Route source mapping is inconsistent.");
        var country = await configuration.GetCountryAsync(service.CountryId, cancellationToken);
        var fields = await configuration.GetReferenceFieldsAsync(service.Id, cancellationToken);
        var connection = route.InquiryProvider is null ? null : connections.Resolve(route.InquiryProvider);
        if (country is null || country.Currency != service.Currency || service.Currency != "USD" ||
            !service.AllowsQuote || !service.AllowsPayment || !ReferenceValidation.ValidDefinitions(fields) ||
            route.InquiryProvider is null || !provider.IsConfigured(route.InquiryProvider) || connection is null ||
            !ValidEndpoint(connection.PaymentEndpoint) || !ValidEndpoint(connection.RollbackEndpoint) ||
            !ValidEndpoint(connection.StatusEndpoint.Replace("{reference}", "reference")) || string.IsNullOrWhiteSpace(connection.CredentialReference))
            return Failure("The route has incomplete or incompatible payment/inquiry configuration.");

        route.CompanyPaymentEndpoint = connection.PaymentEndpoint;
        route.CompanyRollbackEndpoint = connection.RollbackEndpoint;
        route.CompanyStatusEndpoint = connection.StatusEndpoint;
        route.CompanyCredentialReference = connection.CredentialReference;
        await routes.SetActiveAsync(route.Id, cancellationToken);
        service.IsActive = true;
        service.Status = "Active";
        mapping!.MappingStatus = ExternalCatalogueMappingStatus.Mapped;
        mapping.ReviewReason = null;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, service.Id, route.Id, null);
    }

    private static bool ValidEndpoint(string endpoint) => Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) &&
        (uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback);
    private static ActivatePaymentRouteResult Failure(string message) => new(false, null, null, message);
}
