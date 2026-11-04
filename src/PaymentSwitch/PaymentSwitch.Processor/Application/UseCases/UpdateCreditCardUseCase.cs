using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Application.UseCases;

public sealed class UpdateCreditCardUseCase(ICreditCardRepository cards, ICardMutationScope mutations,
	IUnitOfWork unitOfWork, IClock clock)
{
	public async Task<string?> ExecuteAsync(UpdateCreditCardCommand request, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(request.CardHolderName) || request.CardHolderName.Length > 200)
			throw new CardOperationException("INVALID_DISPLAY_NAME", "Supply a display name of 1 to 200 characters.");
		await using var transaction = await mutations.BeginAsync(request.ClientId, ct);
		var card = await cards.GetByIdAsync(request.CardId);
		if (card is null || card.ClientId != request.ClientId) return "Card not found.";
		if (request.IsDefault && !card.IsAvailable(clock.UtcNow))
			throw new CardOperationException("CARD_UNAVAILABLE", "The card cannot be selected as default.", 409);
		if (request.IsDefault)
		{
			await cards.ClearDefaultsAsync(request.ClientId, ct);
			// Flush the old default before the new one under the same transaction and owner lock.
			await unitOfWork.SaveChangesAsync(ct);
		}
		card.Update(request.CardHolderName, request.IsDefault, clock.UtcNow);
		await cards.UpdateAsync(card);
		await transaction.CommitAsync(ct);
		return null;
	}
}
