using PaymentServices.Application.Models;

namespace PaymentServices.Application.Abstractions;

public interface IPaymentExecutionClient
{
	Task<ConfirmQuoteResult> ConfirmQuoteAsync(
		string clientId,
		Guid quoteId,
		Guid creditCardId,
		CancellationToken cancellationToken = default);
}
