using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;
using PayableServices.Domain.Enums;

namespace PayableServices.Application.UseCases;

public sealed class ConfirmQuoteUseCase(
	IQuoteRepository quoteRepository,
	IPaymentExecutionClient paymentExecutionClient,
	IClock clock)
{
	public async Task<ConfirmQuoteResult> ExecuteAsync(
		ConfirmQuoteCommand request,
		CancellationToken cancellationToken = default)
	{
		var quote = await quoteRepository.GetByIdAsync(request.QuoteId, cancellationToken);
		if (quote is null || quote.ClientId != request.ClientId)
		{
			return new ConfirmQuoteResult(false, null, null, null, "Quote not found.");
		}

		if (quote.Status != QuoteStatus.Unpaid)
		{
			return new ConfirmQuoteResult(false, null, null, null, "Quote is not payable.");
		}

		if (clock.UtcNow > quote.ExpiresAt)
		{
			quote.Status = QuoteStatus.Expired;
			await quoteRepository.UpdateAsync(quote, cancellationToken);
			return new ConfirmQuoteResult(false, null, null, null, "Quote expired.");
		}

		var result = await paymentExecutionClient.ConfirmQuoteAsync(
			request.ClientId,
			request.QuoteId,
			request.CreditCardId,
			cancellationToken);

		if (result.Success)
		{
			quote.Status = QuoteStatus.Paid;
			await quoteRepository.UpdateAsync(quote, cancellationToken);
		}
		else
		{
			quote.Status = QuoteStatus.PaymentFailed;
			await quoteRepository.UpdateAsync(quote, cancellationToken);
		}

		return result;
	}
}
