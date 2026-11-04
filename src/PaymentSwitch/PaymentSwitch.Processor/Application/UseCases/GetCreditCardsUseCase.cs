using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Application.UseCases;

public sealed class GetCreditCardsUseCase(ICreditCardRepository cards)
{
	public Task<IReadOnlyList<CreditCard>> ExecuteAsync(string clientId, int page = 1, int pageSize = 20, CancellationToken ct = default)
	{
		if (page is < 1 or > 1_000_000 || pageSize is < 1 or > 100)
			throw new CardOperationException("INVALID_PAGINATION", "Page must be between 1 and 1000000 and page size between 1 and 100.");
		return cards.GetPageAsync(clientId, page, pageSize, ct);
	}
}
