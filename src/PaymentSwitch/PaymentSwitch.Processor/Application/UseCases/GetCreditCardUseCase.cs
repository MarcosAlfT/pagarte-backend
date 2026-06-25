using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Application.UseCases
{
	public sealed class GetCreditCardUseCase(ICreditCardRepository creditCardRepository)
	{
		private readonly ICreditCardRepository _creditCardRepository =
			creditCardRepository;

		public async Task<CreditCard?> ExecuteAsync(string clientId, Guid cardId)
		{
			var card = await _creditCardRepository.GetByIdAsync(cardId);
			if (card == null || card.ClientId != clientId)
			{
				return null;
			}

			return card;
		}
	}
}
