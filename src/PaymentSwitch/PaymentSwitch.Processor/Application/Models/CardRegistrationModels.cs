namespace PaymentSwitch.Processor.Application.Models;

public sealed record CardOperatorIdentity(string IntegrationId, string Provider, string Account, string Environment, string Version);
public sealed record CardMobileInitialization(string Provider, string Method, string Version, string? PublicKey);
public sealed record CardRegistrationView(Guid Id, string State, DateTime ExpiresAt, Guid? CardId,
	string? NextAction, string? ErrorCode, CardMobileInitialization Initialization, string CleanupState);
public sealed record StartCardRegistrationCommand(string ClientId, string IdempotencyKey, string CardHolderName, bool IsDefault, bool ConsentAccepted = false, string ConsentVersion = "");
public sealed record CompleteCardRegistrationCommand(string ClientId, Guid SessionId, string IdempotencyKey, string Token);
public sealed record CardSetupContext(Guid SessionId, string ClientId, CardOperatorIdentity Operator, DateTime ExpiresAt)
{
	public string OperationKey => $"setup-{SessionId:N}";
}
public sealed record VerifiedCardSetupNotification(Guid SessionId, string EventId);
public enum CardSetupOutcome { Succeeded, RequiresAction, Declined, Cancelled, Unknown, NotFound }
public sealed record VerifiedCardEvidence(string Reference, string Last4, string CardType, int ExpiryMonth, int ExpiryYear);
public sealed record CardSetupResult(CardSetupOutcome Outcome, VerifiedCardEvidence? Card = null, string? NextAction = null);

public sealed class CardOperationException(string code, string message, int httpStatus = 400) : Exception(message)
{
	public string Code { get; } = code;
	public int HttpStatus { get; } = httpStatus;
}

public sealed class CardRegistrationOptions
{
	public string ConsentVersion { get; set; } = "saved-card-v1";
}
public sealed record CardRegistrationPolicy(string ConsentVersion, string Purpose);

public enum CardRemovalOutcome { Removed, Pending, Unsupported, Unknown, Rejected }
public sealed record CardRemovalCapabilities(bool Supported, bool RefundsSurviveDetachment);
public sealed record CardRemovalView(Guid Id, bool IsDeleted, string ProviderRemovalState, DateTime? NextAttemptAt);
