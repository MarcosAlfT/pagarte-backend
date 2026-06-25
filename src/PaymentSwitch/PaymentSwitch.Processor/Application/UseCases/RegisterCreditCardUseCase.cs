using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Domain.Enums;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Application.UseCases
{
	public sealed class RegisterCreditCardUseCase(
		ICreditCardRepository creditCardRepository,
		ICreditCardRegistrationGateway creditCardRegistrationGateway,
		IUnitOfWork unitOfWork,
		IClock clock,
		ILogger<RegisterCreditCardUseCase> logger)
	{
		private readonly ICreditCardRepository _creditCardRepository = creditCardRepository;
		private readonly ICreditCardRegistrationGateway _creditCardRegistrationGateway =
			creditCardRegistrationGateway;
		private readonly IUnitOfWork _unitOfWork = unitOfWork;
		private readonly IClock _clock = clock;
		private readonly ILogger<RegisterCreditCardUseCase> _logger = logger;

		public async Task<RegisterCreditCardResult> ExecuteAsync(
			RegisterCreditCardCommand request,
			CancellationToken cancellationToken = default)
		{
			_logger.LogInformation(
				"Registering card for client {ClientId}",
				request.ClientId);

			var result = await _creditCardRegistrationGateway.RegisterAsync(
				request.CardNumber,
				request.Cvv,
				request.CardHolderName,
				request.ExpiryMonth,
				request.ExpiryYear);

			if (!result.Success)
			{
				return new RegisterCreditCardResult(
					false,
					null,
					null,
					result.ErrorMessage ?? "Card registration failed.");
			}

			if (request.IsDefault)
			{
				await ClearDefaultCardsAsync(request.ClientId);
			}

			var cardType = Enum.TryParse<CardType>(
				result.CardType,
				ignoreCase: true,
				out var parsedCardType)
				? parsedCardType
				: CardType.Other;

			var card = CreditCard.Create(
				request.ClientId,
				result.ProviderCode!,
				result.CardToken!,
				request.CardNumber,
				request.CardHolderName,
				result.Last4Digits!,
				cardType,
				result.ExpiryMonth,
				result.ExpiryYear,
				request.IsDefault,
				_clock.UtcNow);

			await _creditCardRepository.CreateAsync(card);
			await _unitOfWork.SaveChangesAsync(cancellationToken);

			_logger.LogInformation(
				"Card registered for client {ClientId} ending in {Last4}",
				request.ClientId,
				result.Last4Digits);

			return new RegisterCreditCardResult(true, card, result.CardType, null);
		}

		private async Task ClearDefaultCardsAsync(string clientId)
		{
			var existing = await _creditCardRepository.GetByClientIdAsync(clientId);
			foreach (var card in existing.Where(c => c.IsDefault))
			{
				card.Update(card.CardHolderName, false, _clock.UtcNow);
				await _creditCardRepository.UpdateAsync(card);
			}
		}
	}
}
