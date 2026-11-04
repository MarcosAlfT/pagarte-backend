using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;

namespace PaymentSwitch.Processor.Application.UseCases;

public sealed class DeleteCreditCardUseCase(ICardDetachmentRepository cards, ICardMutationScope mutations, IClock clock)
{
	public async Task ExecuteAsync(DeleteCreditCardCommand request, CancellationToken ct = default)
	{
		await using var transaction = await mutations.BeginAsync(request.ClientId, ct);
		var card = await cards.GetAsync(request.CardId, ct);
		if (card is null || card.ClientId != request.ClientId || card.IsDeleted) return;
		card.Delete(clock.UtcNow);
		card.DetachmentState = "Pending";
		card.DetachmentNextAttemptAt = clock.UtcNow;
		await transaction.CommitAsync(ct);
	}
}
