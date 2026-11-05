using PayableServices.Application.Models;

namespace PayableServices.Application.Abstractions;

public interface IPaymentExecutionClient
{
	Task<ConfirmQuoteResult> ConfirmQuoteAsync(
		QuoteExecutionPackage package,
		CancellationToken cancellationToken = default);

	Task<ConfirmQuoteResult> RejectUnavailableRouteAsync(
		QuoteExecutionPackage package,
		CancellationToken cancellationToken = default);

	Task<ConfirmQuoteResult> GetStatusAsync(
		string clientId,
		Guid sourceQuoteId,
		CancellationToken cancellationToken = default);
}
