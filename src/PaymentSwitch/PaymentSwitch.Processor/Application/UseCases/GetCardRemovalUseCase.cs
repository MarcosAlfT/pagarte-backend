using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;

namespace PaymentSwitch.Processor.Application.UseCases;

public sealed class GetCardRemovalUseCase(ICardDetachmentRepository cards)
{
	public async Task<CardRemovalView> ExecuteAsync(string clientId, Guid id, CancellationToken ct = default)
	{
		var card = await cards.GetAsync(id, ct);
		if (card is null || card.ClientId != clientId) throw new CardOperationException("CARD_NOT_FOUND", "Card not found.", 404);
		return new(card.Id, card.IsDeleted, card.DetachmentState, card.DetachmentNextAttemptAt);
	}
}
