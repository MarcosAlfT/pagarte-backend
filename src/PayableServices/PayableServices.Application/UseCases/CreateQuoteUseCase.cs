using System.Text.RegularExpressions;
using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;
using PayableServices.Domain.Entities;
using PayableServices.Domain.Enums;
using Utilities.Money;

namespace PayableServices.Application.UseCases;

public sealed class CreateQuoteUseCase(
    IPayableServiceRepository payableServiceRepository,
    IPaymentRouteRepository paymentRouteRepository,
    IQuoteConfigurationRepository quoteConfigurationRepository,
    IQuoteRepository quoteRepository,
    ICurrencyCatalog currencyCatalog,
    IClock clock, IInquiryRepository inquiries, IUnitOfWork unitOfWork)
{
    public async Task<CreateQuoteResult> ExecuteAsync(
        CreateQuoteCommand request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginAsync(request.InquiryId is { } inquiryKey ? "inquiry:" + inquiryKey : "quote:" + Guid.NewGuid(), cancellationToken);
        AmountInquiry? inquiry = null;
        if (request.InquiryId is { } inquiryId)
        {
            inquiry = await inquiries.GetAsync(inquiryId, cancellationToken);
            if (inquiry is null || inquiry.ClientId != request.ClientId) return Failure("Inquiry not found.");
            if ((request.ServiceId != Guid.Empty && request.ServiceId != inquiry.ServiceId) ||
                request.Currency is not null || request.ReferenceValues.Count != 0)
                return Failure("Inquiry snapshot values cannot be overridden.");
            if (inquiry.QuoteId is { } existingId)
            {
                var existing = await quoteRepository.GetByIdAsync(existingId, cancellationToken);
                return existing is null ? Failure("Inquiry quote unavailable.") : new(true, QuoteMapper.Map(existing), null);
            }
            if (!inquiry.CanQuote(request.ClientId, clock.UtcNow)) return Failure("Inquiry is not payable or has expired.");
            request = request with { ServiceId = inquiry.ServiceId, Currency = inquiry.Currency, ReferenceValues = inquiry.References() };
        }

        var service = await payableServiceRepository.GetByIdAsync(
            request.ServiceId,
            cancellationToken);

        if (service is null)
        {
            return Failure("Service not found.");
        }

        if (service.RequiresInquiry && inquiry is null) return Failure("An amount inquiry is required for this service.");

        if (!service.IsActive || !service.AllowsQuote || !service.AllowsPayment)
        {
            return Failure("Service is not available for payment quoting.");
        }

        if (!string.Equals(service.Currency, request.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return Failure("Currency is not valid for this service.");
        }

        var route = await paymentRouteRepository.GetActiveByPayableServiceIdAsync(
            service.Id,
            cancellationToken);
        if (route is null
            || string.IsNullOrWhiteSpace(route.ExternalServiceCode)
            || string.IsNullOrWhiteSpace(route.CompanyPaymentEndpoint)
            || string.IsNullOrWhiteSpace(route.CompanyRollbackEndpoint)
            || string.IsNullOrWhiteSpace(route.CompanyCredentialReference))
        {
            return Failure("The service does not have a complete active payment route.");
        }

        if (inquiry is not null && (route.Id != inquiry.RouteId || route.ExternalServiceCode != inquiry.ExternalServiceCode ||
            route.InquiryProvider != inquiry.Provider || service.Currency != inquiry.Currency))
            return Failure("Inquiry is incompatible with the current service route.");

        var country = await quoteConfigurationRepository.GetCountryAsync(
            service.CountryId,
            cancellationToken);
        if (country is null)
        {
            return Failure("The service country is not active.");
        }

        var countryCode = country.Code.Trim().ToUpperInvariant();
        if (countryCode.Length != 2
            || countryCode.Any(character => character is < 'A' or > 'Z'))
        {
            return Failure("The service country does not have a valid ISO 3166-1 alpha-2 code.");
        }

        if (!string.Equals(country.Currency, service.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return Failure("The service currency does not match its country configuration.");
        }

        var definitions = await quoteConfigurationRepository.GetReferenceFieldsAsync(
            service.Id,
            cancellationToken);
        var referenceValidation = ValidateReferences(definitions, request.ReferenceValues);
        if (referenceValidation.ErrorMessage is not null)
        {
            return Failure(referenceValidation.ErrorMessage);
        }

        try
        {
            var rounding = MidpointRounding.ToEven;
            var composition = await quoteConfigurationRepository.GetAmountCompositionAsync(
                service.Id,
                cancellationToken);
            var configuredComponents = composition?.Components.OrderBy(x => x.DisplayOrder).ToArray()
                ?? [];

            if (configuredComponents.Length == 0)
            {
                configuredComponents =
                [
                    new AmountComponent
                    {
                        ComponentType = "ServiceAmount",
                        Description = service.Name,
                        Amount = service.BaseAmount,
                        Currency = service.Currency,
                        Source = "Catalogue",
                        IsRequired = true,
                        IsVisibleToClient = true,
                        DisplayOrder = 1
                    }
                ];
            }

            if (inquiry is not null)
            {
                // Replace every configured base with exactly one persisted provider value.
                configuredComponents = configuredComponents.Where(x => x.ComponentType is not ("ServiceAmount" or "Value"))
                    .Prepend(new AmountComponent
                    {
                        ComponentType = "ServiceAmount",
                        Description = service.Name,
                        Amount = inquiry.AmountMinorUnits!.Value / 100m,
                        Currency = inquiry.Currency!,
                        Source = "AmountInquiry",
                        IsRequired = true,
                        IsVisibleToClient = true,
                        DisplayOrder = 0
                    }).ToArray();
            }
            if (configuredComponents.Any(component => component.Currency != service.Currency))
                return Failure("Every amount component must match the service currency.");

            if (configuredComponents.Any(component =>
                string.IsNullOrWhiteSpace(component.ComponentType)))
            {
                return Failure("Every amount component must have a component type.");
            }

            var moneyComponents = configuredComponents
                .Select(component => new
                {
                    Component = component,
                    Money = MoneyValue.Create(
                        component.Amount,
                        component.Currency,
                        currencyCatalog,
                        rounding)
                })
                .ToArray();
            var total = MoneyValue.Sum(moneyComponents.Select(x => x.Money));
            var now = clock.UtcNow;

            var quote = new Quote
            {
                Id = Guid.NewGuid(),
                InquiryId = inquiry?.Id,
                ClientId = request.ClientId,
                ServiceId = service.Id,
                ServiceName = service.Name,
                CountryId = service.CountryId,
                CountryCode = countryCode,
                RouteId = route.Id,
                RouteType = route.RouteType,
                PaymentNetworkId = route.PaymentNetworkId,
                ProviderId = route.ProviderId,
                ExternalSourceCode = route.ExternalSourceCode,
                ExternalServiceCode = route.ExternalServiceCode,
                CompanyPaymentEndpoint = route.CompanyPaymentEndpoint,
                CompanyRollbackEndpoint = route.CompanyRollbackEndpoint,
                CompanyStatusEndpoint = route.CompanyStatusEndpoint,
                CompanyCredentialReference = route.CompanyCredentialReference,
                Currency = total.Currency,
                CurrencyScale = total.Scale,
                RoundingMode = rounding.ToString(),
                Status = QuoteStatus.Unpaid,
                CreatedAt = now,
                ExpiresAt = inquiry?.ExpiresAt ?? now.AddMinutes(60),
                TotalAmount = total.Amount,
                TotalMinorUnits = total.MinorUnits
            };

            foreach (var component in moneyComponents)
            {
                quote.Items.Add(new QuoteItem
                {
                    Id = Guid.NewGuid(),
                    QuoteId = quote.Id,
                    ComponentType = component.Component.ComponentType,
                    Description = component.Component.Description,
                    Source = component.Component.Source,
                    IsVisibleToClient = component.Component.IsVisibleToClient,
                    DisplayOrder = component.Component.DisplayOrder,
                    Amount = component.Money.Amount,
                    MinorUnits = component.Money.MinorUnits,
                    Currency = component.Money.Currency
                });
            }

            foreach (var reference in inquiry is null ? referenceValidation.Values : inquiry.References())
            {
                quote.ReferenceValues.Add(new QuoteReferenceValue
                {
                    Id = Guid.NewGuid(),
                    QuoteId = quote.Id,
                    Key = reference.Key,
                    Value = reference.Value
                });
            }

            if (inquiry is not null)
            {
                if (!inquiry.CanQuote(request.ClientId, clock.UtcNow)) return Failure("Inquiry expired during quote creation.");
                inquiry.QuoteId = quote.Id;
            }
            await quoteRepository.CreateAsync(quote, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new CreateQuoteResult(true, QuoteMapper.Map(quote), null);
        }
        catch (MoneyValidationException exception)
        {
            return Failure(exception.Message);
        }
        catch (OverflowException)
        {
            return Failure("The quote total is outside the supported monetary range.");
        }
    }

    private static (IReadOnlyCollection<KeyValuePair<string, string>> Values, string? ErrorMessage)
        ValidateReferences(
            IReadOnlyCollection<ReferenceFieldDefinition> definitions,
            IReadOnlyDictionary<string, string> suppliedValues)
    {
        var definitionMap = definitions.ToDictionary(x => x.FieldKey, StringComparer.Ordinal);
        var unknownKey = suppliedValues.Keys.FirstOrDefault(key => !definitionMap.ContainsKey(key));
        if (unknownKey is not null)
        {
            return ([], $"Reference field '{unknownKey}' is not valid for this service.");
        }

        var normalized = new List<KeyValuePair<string, string>>();
        foreach (var definition in definitions.OrderBy(x => x.DisplayOrder))
        {
            suppliedValues.TryGetValue(definition.FieldKey, out var value);
            value = value?.Trim();

            if (definition.IsRequired && string.IsNullOrEmpty(value))
            {
                return ([], $"Reference field '{definition.FieldKey}' is required.");
            }

            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            if ((definition.MinLength is not null && value.Length < definition.MinLength)
                || (definition.MaxLength is not null && value.Length > definition.MaxLength))
            {
                return ([], $"Reference field '{definition.FieldKey}' has an invalid length.");
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(definition.ValidationRule)
                    && !Regex.IsMatch(
                        value,
                        definition.ValidationRule,
                        RegexOptions.CultureInvariant,
                        TimeSpan.FromMilliseconds(250)))
                {
                    return ([], $"Reference field '{definition.FieldKey}' has an invalid value.");
                }
            }
            catch (RegexMatchTimeoutException)
            {
                return ([], $"Reference field '{definition.FieldKey}' could not be validated safely.");
            }

            normalized.Add(new KeyValuePair<string, string>(definition.FieldKey, value));
        }

        return (normalized, null);
    }

    private static CreateQuoteResult Failure(string errorMessage) =>
        new(false, null, errorMessage);
}
