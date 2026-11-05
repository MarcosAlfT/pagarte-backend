using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PaymentSwitch.Processor.Application.Models;
using Utilities.Money;

namespace PaymentSwitch.Processor.Application.Services;

public sealed class ConfirmedQuoteValidator(ICurrencyCatalog currencyCatalog)
{
	public string? Validate(ConfirmedQuoteExecution execution, DateTime utcNow)
	{
		if (execution.SourceQuoteId == Guid.Empty
			|| execution.CreditCardId == Guid.Empty
			|| execution.ServiceId == Guid.Empty
			|| execution.CountryId == Guid.Empty
			|| execution.RouteId == Guid.Empty)
		{
			return "Execution identifiers must be non-empty.";
		}

		if (string.IsNullOrWhiteSpace(execution.ClientId)
			|| string.IsNullOrWhiteSpace(execution.ServiceName)
			|| string.IsNullOrWhiteSpace(execution.ExternalServiceCode)
			|| string.IsNullOrWhiteSpace(execution.CompanyCredentialReference))
		{
			return "The execution snapshot is incomplete.";
		}

		if (execution.CountryCode.Length != 2
			|| execution.CountryCode.Any(character => character is < 'A' or > 'Z'))
		{
			return "Country code must be an uppercase ISO 3166-1 alpha-2 code.";
		}

		if (!IsHttpEndpoint(execution.CompanyPaymentEndpoint)
			|| !IsHttpEndpoint(execution.CompanyRollbackEndpoint)
			|| !string.IsNullOrWhiteSpace(execution.CompanyStatusEndpoint)
				&& !IsHttpEndpoint(execution.CompanyStatusEndpoint))
		{
			return "Company endpoints must be absolute HTTP or HTTPS addresses.";
		}

		if (execution.QuoteCreatedAt >= execution.QuoteExpiresAt)
		{
			return "The confirmed quote has invalid timestamps.";
		}

        if (execution.SubmissionId is not null || execution.SubmittedAt is not null)
        {
            if (execution.SubmissionId is null || execution.SubmissionId == Guid.Empty)
                return "Submission identifier is required.";
            if (execution.SubmittedAt is not { } submittedAt ||
                submittedAt.Kind != DateTimeKind.Utc || execution.QuoteCreatedAt.Kind != DateTimeKind.Utc ||
                execution.QuoteExpiresAt - execution.QuoteCreatedAt != TimeSpan.FromMinutes(5) ||
                submittedAt < execution.QuoteCreatedAt || submittedAt >= execution.QuoteExpiresAt || submittedAt > utcNow)
                return "Submission must be a server UTC claim within the five-minute quote window.";
        }
        else if (execution.QuoteExpiresAt <= utcNow)
        {
            return "The confirmed quote is expired.";
        }

		if (!Enum.TryParse<MidpointRounding>(
			execution.RoundingMode,
			ignoreCase: false,
			out var rounding)
			|| rounding != MidpointRounding.ToEven)
		{
			return "The execution snapshot uses an unsupported rounding rule.";
		}

		if (execution.TotalMinorUnits <= 0 || execution.Components.Count == 0)
		{
			return "The execution total and components must be present and positive.";
		}

		try
		{
			var definition = currencyCatalog.GetRequired(execution.Currency);
			var componentTotal = 0L;
			foreach (var component in execution.Components)
			{
				if (string.IsNullOrWhiteSpace(component.ComponentType)
					|| component.Currency != definition.Code)
				{
					return "Every amount component requires a type and the quote currency.";
				}

				componentTotal = checked(componentTotal + component.MinorUnits);
			}

			if (componentTotal != execution.TotalMinorUnits)
			{
				return "The quote total does not equal the checked sum of its components.";
			}
		}
		catch (MoneyValidationException exception)
		{
			return exception.Message;
		}
		catch (OverflowException)
		{
			return "The component total is outside the supported monetary range.";
		}

		if (execution.ReferenceValues
			.GroupBy(reference => reference.Key, StringComparer.Ordinal)
			.Any(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1))
		{
			return "Reference values must have non-empty unique keys.";
		}

		return null;
	}

	public static string Fingerprint(ConfirmedQuoteExecution execution)
	{
		var canonical = new
		{
			execution.ClientId,
			execution.SourceQuoteId,
			execution.CreditCardId,
			execution.ServiceId,
			execution.ServiceName,
			execution.CountryId,
			execution.CountryCode,
			execution.RouteId,
			execution.RouteType,
			execution.PaymentNetworkId,
			execution.ProviderId,
			execution.ExternalSourceCode,
			execution.ExternalServiceCode,
			execution.CompanyPaymentEndpoint,
			execution.CompanyRollbackEndpoint,
			execution.CompanyStatusEndpoint,
			execution.CompanyCredentialReference,
			execution.QuoteCreatedAt,
			execution.QuoteExpiresAt,
			execution.RoundingMode,
			execution.TotalMinorUnits,
			execution.Currency,
			Components = execution.Components
				.OrderBy(component => component.DisplayOrder)
				.ThenBy(component => component.ComponentType, StringComparer.Ordinal),
			ReferenceValues = execution.ReferenceValues
				.OrderBy(reference => reference.Key, StringComparer.Ordinal)
		};
		var bytes = execution.BillKey is not null
            ? JsonSerializer.SerializeToUtf8Bytes(new { execution.SubmissionId, execution.SubmittedAt, execution.BillKey, Snapshot = canonical })
            : execution.SubmissionId is null && execution.SubmittedAt is null
            ? JsonSerializer.SerializeToUtf8Bytes(canonical)
            : JsonSerializer.SerializeToUtf8Bytes(new { execution.SubmissionId, execution.SubmittedAt, Snapshot = canonical });
		return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
	}

	private static bool IsHttpEndpoint(string endpoint) =>
		Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
		&& uri.Scheme is "http" or "https";
}
