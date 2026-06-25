using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.Abstractions
{
	public interface ICardAuthorizationGateway
	{
		Task<string> EnsureOperatorProviderAsync(CreditCard card);

		Task<CardAuthorizationResult> AuthorizeAsync(
			string operatorProvider,
			string operatorCardToken,
			decimal amount,
			string currency,
			string reference);
	}

	public sealed record CardAuthorizationResult(
		bool Success,
		string? OperatorPaymentId,
		string? ErrorMessage);
}
