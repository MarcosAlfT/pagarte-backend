using Grpc.Core;
using PaymentSwitch.Contracts;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Application.UseCases;

namespace PaymentSwitch.Processor.GrpcServices;

public sealed class PaymentExecutionGrpcService(
	ConfirmPaymentQuoteUseCase confirmPaymentQuoteUseCase,
	ILogger<PaymentExecutionGrpcService> logger)
	: PaymentExecutionService.PaymentExecutionServiceBase
{
	private readonly ConfirmPaymentQuoteUseCase _confirmPaymentQuoteUseCase =
		confirmPaymentQuoteUseCase;
	private readonly ILogger<PaymentExecutionGrpcService> _logger = logger;

	public override async Task<ConfirmQuoteResponse> ConfirmQuote(
		ConfirmQuoteRequest request,
		ServerCallContext context)
	{
		_logger.LogInformation(
			"Confirming quote {QuoteId} for client {ClientId}",
			request.QuoteId,
			request.ClientId);

		var result = await _confirmPaymentQuoteUseCase.ExecuteAsync(
			new ConfirmPaymentCommand(
				request.ClientId,
				Guid.Parse(request.QuoteId),
				Guid.Parse(request.CreditCardId)),
			context.CancellationToken);

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
