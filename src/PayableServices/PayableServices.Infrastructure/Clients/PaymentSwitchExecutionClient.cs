using Grpc.Core;
using Microsoft.Extensions.Configuration;
using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;
using PaymentSwitch.Contracts;

namespace PayableServices.Infrastructure.Clients;

public sealed class PaymentSwitchExecutionClient(
	PaymentExecutionService.PaymentExecutionServiceClient client,
	IConfiguration configuration) : IPaymentExecutionClient
{
	private readonly string _workloadApiKey =
		configuration["WorkloadAuth:ApiKey"]
		?? throw new InvalidOperationException("WorkloadAuth:ApiKey is not configured.");

	public async Task<ConfirmQuoteResult> ConfirmQuoteAsync(
		QuoteExecutionPackage package,
		CancellationToken cancellationToken = default)
	{
        var request = MapRequest(package);
        if (package.SubmissionId is null || package.SubmittedAt is null)
            return MapResponse(await client.ConfirmQuoteAsync(request, CreateHeaders(), cancellationToken: cancellationToken));

        var submission = MapSubmission(package, request);
        if (package.BillVerification is not { } verification)
            return MapResponse(await client.SubmitClaimedQuoteAsync(submission, CreateHeaders(), cancellationToken: cancellationToken));

        return MapResponse(await client.SubmitVerifiedBillAsync(new SubmitVerifiedBillRequest
        {
            Submission = submission,
            BillKey = package.BillKey ?? string.Empty,
            Verification = new BillVerificationEvidence
            {
                ErrorCode = verification.ErrorCode ?? string.Empty,
                ProviderInquiryId = verification.ProviderInquiryId ?? string.Empty,
                ObservedBillKey = verification.ObservedBillKey ?? string.Empty,
                BillingPeriod = verification.BillingPeriod ?? string.Empty,
                CheckedAt = verification.CheckedAt?.ToString("O") ?? string.Empty,
                ExpiresAt = verification.ExpiresAt?.ToString("O") ?? string.Empty,
                ServiceAmount = new Money
                {
                    MinorUnits = verification.AmountMinorUnits ?? 0,
                    CurrencyCode = verification.Currency ?? string.Empty
                }
            }
        }, CreateHeaders(), cancellationToken: cancellationToken));
	}

    public async Task<ConfirmQuoteResult> RejectUnavailableRouteAsync(
        QuoteExecutionPackage package, CancellationToken cancellationToken = default) =>
        MapResponse(await client.RejectUnavailableQuoteRouteAsync(MapSubmission(package, MapRequest(package)),
            CreateHeaders(), cancellationToken: cancellationToken));

    private static ConfirmQuoteRequest MapRequest(QuoteExecutionPackage package) => new()
    {
        ClientId = package.ClientId,
        SourceQuoteId = package.SourceQuoteId.ToString(),
        CreditCardId = package.CreditCardId.ToString(),
        Snapshot = MapSnapshot(package)
    };

    private static SubmitClaimedQuoteRequest MapSubmission(QuoteExecutionPackage package, ConfirmQuoteRequest request) => new()
    {
        Quote = request,
        SubmissionId = package.SubmissionId?.ToString() ?? throw new InvalidOperationException("Submission ID is missing."),
        SubmittedAt = package.SubmittedAt?.ToString("O") ?? throw new InvalidOperationException("Submission time is missing.")
    };

	public async Task<ConfirmQuoteResult> GetStatusAsync(
		string clientId,
		Guid sourceQuoteId,
		CancellationToken cancellationToken = default)
	{
		var response = await client.GetExecutionStatusAsync(
			new GetExecutionStatusRequest
			{
				ClientId = clientId,
				SourceQuoteId = sourceQuoteId.ToString()
			},
			CreateHeaders(),
			cancellationToken: cancellationToken);
		return MapResponse(response);
	}

	private Metadata CreateHeaders() =>
		new() { { "x-workload-api-key", _workloadApiKey } };

	private static QuoteExecutionSnapshot MapSnapshot(QuoteExecutionPackage package)
	{
		var snapshot = new QuoteExecutionSnapshot
		{
			ServiceId = package.ServiceId.ToString(),
			ServiceName = package.ServiceName,
			CountryId = package.CountryId.ToString(),
			CountryCode = package.CountryCode,
			RouteId = package.RouteId.ToString(),
			RouteType = package.RouteType,
			PaymentNetworkId = package.PaymentNetworkId?.ToString() ?? string.Empty,
			ProviderId = package.ProviderId?.ToString() ?? string.Empty,
			ExternalSourceCode = package.ExternalSourceCode ?? string.Empty,
			ExternalServiceCode = package.ExternalServiceCode,
			CompanyPaymentEndpoint = package.CompanyPaymentEndpoint,
			CompanyRollbackEndpoint = package.CompanyRollbackEndpoint,
			CompanyStatusEndpoint = package.CompanyStatusEndpoint ?? string.Empty,
			CompanyCredentialReference = package.CompanyCredentialReference,
			CreatedAt = package.CreatedAt.ToString("O"),
			ExpiresAt = package.ExpiresAt.ToString("O"),
			RoundingMode = package.RoundingMode,
			Total = new Money
			{
				CurrencyCode = package.Currency,
				MinorUnits = package.TotalMinorUnits
			}
		};

		snapshot.Components.AddRange(package.Components.Select(component =>
			new QuoteAmountComponent
			{
				ComponentType = component.ComponentType,
				Description = component.Description,
				Source = component.Source,
				VisibleToClient = component.VisibleToClient,
				DisplayOrder = component.DisplayOrder,
				Amount = new Money
				{
					CurrencyCode = component.Currency,
					MinorUnits = component.MinorUnits
				}
			}));
		snapshot.ReferenceValues.AddRange(package.ReferenceValues.Select(reference =>
			new PaymentSwitch.Contracts.QuoteReferenceValue
			{
				Key = reference.Key,
				Value = reference.Value
			}));
		return snapshot;
	}

	private static ConfirmQuoteResult MapResponse(ConfirmQuoteResponse response) =>
		new(
			response.Accepted,
			Guid.TryParse(response.PaymentId, out var paymentId) ? paymentId : null,
			EmptyToNull(response.Reference),
			response.Status switch
			{
				ExecutionStatus.Completed => QuoteExecutionStatus.Completed,
				ExecutionStatus.Declined => QuoteExecutionStatus.Declined,
				ExecutionStatus.RollbackPending => QuoteExecutionStatus.RollbackPending,
				ExecutionStatus.Failed => QuoteExecutionStatus.Failed,
				ExecutionStatus.RefundPending => QuoteExecutionStatus.RefundPending,
				ExecutionStatus.Refunded => QuoteExecutionStatus.Refunded,
				ExecutionStatus.ManualIntervention => QuoteExecutionStatus.ManualIntervention,
				_ => QuoteExecutionStatus.Processing
			},
			EmptyToNull(response.ErrorCode),
			EmptyToNull(response.ErrorMessage),
			response.Replayed,
			response.Final,
            response.SubmissionRecorded);

	private static string? EmptyToNull(string value) =>
		string.IsNullOrWhiteSpace(value) ? null : value;
}
