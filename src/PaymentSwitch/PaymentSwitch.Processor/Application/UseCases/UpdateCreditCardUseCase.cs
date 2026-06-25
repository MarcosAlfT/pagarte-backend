using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Application.UseCases
{
	public sealed class UpdateCreditCardUseCase(
		ICreditCardRepository creditCardRepository,
		IUnitOfWork unitOfWork,
		IClock clock)
	{
		private readonly ICreditCardRepository _creditCardRepository = creditCardRepository;
		private readonly IUnitOfWork _unitOfWork = unitOfWork;
		private readonly IClock _clock = clock;

		public async Task<string?> ExecuteAsync(
			UpdateCreditCardCommand request,
			CancellationToken cancellationToken = default)
		{
			var card = await _creditCardRepository.GetByIdAsync(request.CardId);
			if (card == null || card.ClientId != request.ClientId)
			{
				return "Card not found.";
			}

			if (request.IsDefault)
			{
				var existing = await _creditCardRepository.GetByClientIdAsync(
					request.ClientId);

				foreach (var existingCard in existing.Where(c =>
					c.IsDefault && c.Id != card.Id))
				{
					existingCard.Update(
						existingCard.CardHolderName,
						false,
						_clock.UtcNow);
					await _creditCardRepository.UpdateAsync(existingCard);
				}
			}

			card.Update(request.CardHolderName, request.IsDefault, _clock.UtcNow);
			await _creditCardRepository.UpdateAsync(card);
			await _unitOfWork.SaveChangesAsync(cancellationToken);

			return null;
		}
	}
}
