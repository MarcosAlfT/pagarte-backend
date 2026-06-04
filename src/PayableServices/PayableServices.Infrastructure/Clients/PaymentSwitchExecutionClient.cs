using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;
using PaymentSwitch.Contracts;

namespace PayableServices.Infrastructure.Clients;

public sealed class PaymentSwitchExecutionClient(
	PaymentExecutionService.PaymentExecutionServiceClient paymentExecutionServiceClient)
	: IPaymentExecutionClient
{
	private readonly PaymentExecutionService.PaymentExecutionServiceClient _paymentServiceClient = paymentExecutionServiceClient;

	public async Task<ConfirmQuoteResult> ConfirmQuoteAsync(
		string clientId,
		Guid quoteId,
		Guid creditCardId,
		CancellationToken cancellationToken = default)
	{
		var response = await _paymentServiceClient.ConfirmQuoteAsync(
			new ConfirmQuoteRequest
			{
				ClientId = clientId,
				QuoteId = quoteId.ToString(),
				CreditCardId = creditCardId.ToString()
			});

		return new ConfirmQuoteResult(
			response.Success,
			Guid.TryParse(response.PaymentId, out var paymentId) ? paymentId : null,
			string.IsNullOrWhiteSpace(response.Reference) ? null : response.Reference,
			string.IsNullOrWhiteSpace(response.Status) ? null : response.Status,
			string.IsNullOrWhiteSpace(response.ErrorMessage) ? null : response.ErrorMessage);
	}
}
