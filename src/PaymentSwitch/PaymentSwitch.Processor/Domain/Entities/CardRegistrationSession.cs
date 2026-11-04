namespace PaymentSwitch.Processor.Domain.Entities;

public enum CardRegistrationState { Pending, Verifying, RequiresAction, Succeeded, Failed, Cancelled, Expired, Unresolved }

public sealed class CardRegistrationSession
{
	public Guid Id { get; set; }
	public string ClientId { get; set; } = "";
	public string CreationKey { get; set; } = "";
	public string CreationFingerprint { get; set; } = "";
	public string? CompletionKey { get; set; }
	public string? TokenDigest { get; set; }
	public string? ProtectedToken { get; set; }
	public string IntegrationId { get; set; } = "";
	public string Provider { get; set; } = "";
	public string ProviderAccount { get; set; } = "";
	public string ProviderEnvironment { get; set; } = "";
	public string IntegrationVersion { get; set; } = "";
	public string CardHolderName { get; set; } = "";
	public bool MakeDefault { get; set; }
	public string? ConsentVersion { get; set; }
	public DateTime? ConsentAcceptedAt { get; set; }
	public CardRegistrationState State { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime ExpiresAt { get; set; }
	public DateTime? TokenExpiresAt { get; set; }
	public DateTime? NextAttemptAt { get; set; }
	public DateTime? LeaseUntil { get; set; }
	public Guid? LeaseId { get; set; }
	public int Attempts { get; set; }
	public string CleanupState { get; set; } = "None";
	public int CleanupAttempts { get; set; }
	public Guid? CardId { get; set; }
	public string? NextAction { get; set; }
	public string? ErrorCode { get; set; }
	public byte[] Version { get; set; } = [];
	public bool IsTerminal => State is CardRegistrationState.Succeeded or CardRegistrationState.Failed
		or CardRegistrationState.Cancelled or CardRegistrationState.Expired;

	public void Finish(CardRegistrationState state, string? errorCode = null)
	{
		if (IsTerminal) return;
		State = state;
		ErrorCode = errorCode;
		ProtectedToken = null;
		NextAction = null;
		if ((state is CardRegistrationState.Cancelled or CardRegistrationState.Expired) && Attempts > 0)
		{
			CleanupState = "Pending";
			NextAttemptAt = LeaseUntil ?? CreatedAt;
		}
		else
		{
			NextAttemptAt = null;
			LeaseUntil = null;
			LeaseId = null;
		}
	}
}
