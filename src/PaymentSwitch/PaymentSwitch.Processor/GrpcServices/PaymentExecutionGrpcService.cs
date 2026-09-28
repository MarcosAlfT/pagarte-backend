using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using PaymentSwitch.Contracts;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Application.UseCases;
using PaymentSwitch.Processor.Security;

namespace PaymentSwitch.Processor.GrpcServices;

[Authorize(AuthenticationSchemes = WorkloadApiKeyAuthenticationHandler.SchemeName)]
public sealed class PaymentExecutionGrpcService(
	AcceptConfirmedQuoteUseCase acceptConfirmedQuoteUseCase,
	GetPaymentExecutionStatusUseCase getPaymentExecutionStatusUseCase,
	ILogger<PaymentExecutionGrpcService> logger)
	: PaymentExecutionService.PaymentExecutionServiceBase
{
	public override async Task<ConfirmQuoteResponse> ConfirmQuote(
		ConfirmQuoteRequest request,
		ServerCallContext context)
	{
		try
		{
			var execution = MapExecution(request);
			logger.LogInformation(
				"Accepting confirmed source quote {SourceQuoteId} for client {ClientId}.",
				execution.SourceQuoteId,
				execution.ClientId);
			return MapResult(await acceptConfirmedQuoteUseCase.ExecuteAsync(
				execution,
				context.CancellationToken));
		}
		catch (FormatException exception)
		{
			return InvalidRequest(exception.Message);
		}
	}

	public override async Task<ConfirmQuoteResponse> GetExecutionStatus(
		GetExecutionStatusRequest request,
		ServerCallContext context)
	{
		if (!Guid.TryParse(request.SourceQuoteId, out var sourceQuoteId))
		{
			return InvalidRequest("Source quote identifier is invalid.");
		}

		return MapResult(await getPaymentExecutionStatusUseCase.ExecuteAsync(
			request.ClientId,
			sourceQuoteId,
			context.CancellationToken));
	}

	private static ConfirmedQuoteExecution MapExecution(ConfirmQuoteRequest request)
	{
		var snapshot = request.Snapshot
			?? throw new FormatException("Execution snapshot is required.");
		return new ConfirmedQuoteExecution(
			request.ClientId,
			ParseGuid(request.SourceQuoteId, "source quote"),
			ParseGuid(request.CreditCardId, "credit card"),
			ParseGuid(snapshot.ServiceId, "service"),
			snapshot.ServiceName,
			ParseGuid(snapshot.CountryId, "country"),
			snapshot.CountryCode,
			ParseGuid(snapshot.RouteId, "route"),
			snapshot.RouteType,
			ParseOptionalGuid(snapshot.PaymentNetworkId, "payment network"),
			ParseOptionalGuid(snapshot.ProviderId, "provider"),
			EmptyToNull(snapshot.ExternalSourceCode),
			snapshot.ExternalServiceCode,
			snapshot.CompanyPaymentEndpoint,
			snapshot.CompanyRollbackEndpoint,
			EmptyToNull(snapshot.CompanyStatusEndpoint),
			snapshot.CompanyCredentialReference,
			ParseDate(snapshot.CreatedAt, "created"),
			ParseDate(snapshot.ExpiresAt, "expiration"),
			snapshot.RoundingMode,
			snapshot.Total?.MinorUnits ?? 0,
			snapshot.Total?.CurrencyCode ?? string.Empty,
			snapshot.Components.Select(component =>
				new ConfirmedQuoteComponent(
					component.ComponentType,
					component.Description,
					component.Source,
					component.VisibleToClient,
					component.DisplayOrder,
					component.Amount?.MinorUnits ?? 0,
					component.Amount?.CurrencyCode ?? string.Empty)).ToArray(),
			snapshot.ReferenceValues.Select(reference =>
				new ConfirmedQuoteReference(reference.Key, reference.Value)).ToArray());
	}

	private static ConfirmQuoteResponse MapResult(PaymentExecutionResult result) =>
		new()
		{
			Accepted = result.Accepted,
			PaymentId = result.PaymentId?.ToString() ?? string.Empty,
			Reference = result.Reference ?? string.Empty,
			Status = result.Status switch
			{
				PaymentExecutionResultStatus.Completed => ExecutionStatus.Completed,
				PaymentExecutionResultStatus.Declined => ExecutionStatus.Declined,
				PaymentExecutionResultStatus.RollbackPending => ExecutionStatus.RollbackPending,
				PaymentExecutionResultStatus.Failed => ExecutionStatus.Failed,
				PaymentExecutionResultStatus.ManualIntervention => ExecutionStatus.ManualIntervention,
				_ => ExecutionStatus.Processing
			},
			ErrorCode = result.ErrorCode ?? string.Empty,
			ErrorMessage = result.ErrorMessage ?? string.Empty,
			Replayed = result.Replayed,
			Final = result.Final
		};

	private static ConfirmQuoteResponse InvalidRequest(string message) =>
		new()
		{
			Accepted = false,
			Status = ExecutionStatus.Failed,
			ErrorCode = "INVALID_EXECUTION_REQUEST",
			ErrorMessage = message,
			Final = true
		};

	private static Guid ParseGuid(string value, string name) =>
		Guid.TryParse(value, out var parsed)
			? parsed
			: throw new FormatException($"The {name} identifier is invalid.");

	private static Guid? ParseOptionalGuid(string value, string name) =>
		string.IsNullOrWhiteSpace(value) ? null : ParseGuid(value, name);

	private static DateTime ParseDate(string value, string name) =>
		DateTime.TryParse(
			value,
			System.Globalization.CultureInfo.InvariantCulture,
			System.Globalization.DateTimeStyles.RoundtripKind,
			out var parsed)
			? parsed.ToUniversalTime()
			: throw new FormatException($"The quote {name} timestamp is invalid.");

	private static string? EmptyToNull(string value) =>
		string.IsNullOrWhiteSpace(value) ? null : value;
}
