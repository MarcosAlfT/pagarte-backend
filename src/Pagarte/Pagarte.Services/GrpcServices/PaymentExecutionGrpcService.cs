using Grpc.Core;
using Pagarte.Contracts;
using Pagarte.Services.Services;

namespace Pagarte.Services.GrpcServices;

public sealed class PaymentExecutionGrpcService(
	PaymentEngineService paymentEngine,
	ILogger<PaymentExecutionGrpcService> logger)
	: PaymentExecutionService.PaymentExecutionServiceBase
{
	private readonly PaymentEngineService _paymentEngine = paymentEngine;
	private readonly ILogger<PaymentExecutionGrpcService> _logger = logger;

	public override async Task<ConfirmQuoteResponse> ConfirmQuote(
		ConfirmQuoteRequest request,
		ServerCallContext context)
	{
		_logger.LogInformation(
			"Confirming quote {QuoteId} for client {ClientId}",
			request.QuoteId,
			request.ClientId);

		var result = await _paymentEngine.ConfirmAsync(
			request.ClientId,
			Guid.Parse(request.QuoteId),
			Guid.Parse(request.CreditCardId));

		return new ConfirmQuoteResponse
		{
			Success = result.Success,
			PaymentId = result.PaymentId?.ToString() ?? string.Empty,
			Reference = result.Reference ?? string.Empty,
			Status = result.Status ?? string.Empty,
			ErrorMessage = result.ErrorMessage ?? string.Empty
		};
	}
}
